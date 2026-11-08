using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HoneyDrunk.Operator.Data;

/// <summary>
/// The durable <see cref="IOperatorStore"/>. Each call uses its own context from the factory, so the store is safe as
/// a singleton. Compare-and-set and spend additions are single SQL statements, so they stay atomic across hosts that
/// share the database.
/// </summary>
/// <param name="contexts">Creates a context per call.</param>
public sealed class OperatorDataService(IDbContextFactory<OperatorDbContext> contexts) : IOperatorStore
{
    private const decimal MicrosPerUnit = 1_000_000m;

    private readonly IDbContextFactory<OperatorDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <inheritdoc />
    public async Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await context.Approvals.AsNoTracking().SingleOrDefaultAsync(approval => approval.ApprovalId == approvalId, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ToStored(row);
    }

    /// <inheritdoc />
    public async Task<bool> TryAddApprovalAsync(StoredApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.Approvals.AnyAsync(row => row.ApprovalId == approval.Request.ApprovalId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        context.Approvals.Add(ToRow(approval));
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // Another host added the same id between the check and the insert; its row is kept.
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var updated = await context.Approvals
            .Where(row => row.ApprovalId == approvalId && row.Outcome == (int)expected)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Outcome, (int)decision.Outcome)
                    .SetProperty(row => row.ApproverIdentity, decision.ApproverIdentity)
                    .SetProperty(row => row.DecidedAtTicks, decision.DecidedAt.UtcTicks)
                    .SetProperty(row => row.Reason, decision.Reason),
                cancellationToken)
            .ConfigureAwait(false);
        return updated == 1;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var nowTicks = now.UtcTicks;
        var rows = await context.Approvals.AsNoTracking()
            .Where(row => row.Outcome == (int)ApprovalOutcome.Pending && row.ExpiryTicks > nowTicks)
            .OrderByDescending(row => row.RequestedAtTicks)
            .ThenBy(row => row.ApprovalId)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToStored).ToArray();
    }

    /// <inheritdoc />
    public async Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await context.Pauses.AsNoTracking().SingleOrDefaultAsync(pause => pause.Name == name, cancellationToken).ConfigureAwait(false);
        return row is null ? null : new PauseState(row.Name, row.PausedBy, row.Reason, FromTicks(row.SinceTicks));
    }

    /// <inheritdoc />
    public async Task SetPauseAsync(PauseState pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var updated = await context.Pauses
            .Where(row => row.Name == pause.Name)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.PausedBy, pause.PausedBy)
                    .SetProperty(row => row.Reason, pause.Reason)
                    .SetProperty(row => row.SinceTicks, pause.Since.UtcTicks),
                cancellationToken)
            .ConfigureAwait(false);
        if (updated == 1)
        {
            return;
        }

        context.Pauses.Add(new PauseEntity { Name = pause.Name, PausedBy = pause.PausedBy, Reason = pause.Reason, SinceTicks = pause.Since.UtcTicks });
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Another host paused it first; a pause is in place either way.
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemovePauseAsync(string name, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Pauses.Where(row => row.Name == name).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc />
    public async Task<decimal> AddSpendAsync(string scope, string window, decimal amount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var micros = ToMicros(amount);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var updated = await context.Spend
                .Where(row => row.Scope == scope && row.Window == window)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AmountMicros, row => row.AmountMicros + micros), cancellationToken)
                .ConfigureAwait(false);
            if (updated == 1)
            {
                return await this.GetSpendAsync(scope, window, cancellationToken).ConfigureAwait(false);
            }

            context.Spend.Add(new SpendEntity { Scope = scope, Window = window, AmountMicros = micros });
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return amount;
            }
            catch (DbUpdateException)
            {
                // Another writer created the row first; add to it on the next pass.
                context.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException($"Spend for '{scope}' in '{window}' could not be recorded after repeated conflicts.");
    }

    /// <inheritdoc />
    public async Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var micros = await context.Spend.AsNoTracking()
            .Where(row => row.Scope == scope && row.Window == window)
            .Select(row => (long?)row.AmountMicros)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return (micros ?? 0) / MicrosPerUnit;
    }

    private static long ToMicros(decimal amount) => (long)decimal.Round(amount * MicrosPerUnit, MidpointRounding.AwayFromZero);

    private static DateTimeOffset FromTicks(long ticks) => new(ticks, TimeSpan.Zero);

    private static ApprovalEntity ToRow(StoredApproval approval) => new()
    {
        ApprovalId = approval.Request.ApprovalId,
        Subject = approval.Request.Subject,
        Action = approval.Request.Action,
        ContextJson = JsonSerializer.Serialize(approval.Request.Context),
        RequestedScope = approval.Request.RequestedScope,
        ExpiryTicks = approval.Request.Expiry.UtcTicks,
        RequesterCorrelationId = approval.Request.RequesterCorrelationId,
        RequestedAtTicks = approval.RequestedAt.UtcTicks,
        Outcome = (int)approval.Decision.Outcome,
        ApproverIdentity = approval.Decision.ApproverIdentity,
        DecidedAtTicks = approval.Decision.DecidedAt.UtcTicks,
        Reason = approval.Decision.Reason,
    };

    private static StoredApproval ToStored(ApprovalEntity row)
    {
        var context = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ContextJson) ?? [];
        var request = new ApprovalRequest(row.ApprovalId, row.Subject, row.Action, context, row.RequestedScope, FromTicks(row.ExpiryTicks), row.RequesterCorrelationId);
        var decision = new ApprovalDecision(row.ApprovalId, (ApprovalOutcome)row.Outcome, row.ApproverIdentity, FromTicks(row.DecidedAtTicks), row.Reason);
        return new StoredApproval(request, decision, FromTicks(row.RequestedAtTicks));
    }
}

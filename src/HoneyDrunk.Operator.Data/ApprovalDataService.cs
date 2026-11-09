using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>Reads and writes approval requests and their decisions. Each call uses its own context from the factory.</summary>
/// <param name="contexts">Creates a context per call.</param>
public sealed class ApprovalDataService(IDbContextFactory<OperatorDbContext> contexts)
{
    private readonly IDbContextFactory<OperatorDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <inheritdoc cref="IOperatorStore.GetApprovalAsync" />
    public async Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await context.Approvals.AsNoTracking().SingleOrDefaultAsync(approval => approval.ApprovalId == approvalId, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ApprovalRows.ToStored(row);
    }

    /// <inheritdoc cref="IOperatorStore.TryAddApprovalAsync" />
    public async Task<bool> TryAddApprovalAsync(StoredApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.Approvals.AnyAsync(row => row.ApprovalId == approval.Request.ApprovalId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        context.Approvals.Add(ApprovalRows.ToRow(approval));
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException) when (this.ApprovalExists(approval.Request.ApprovalId))
        {
            // Another host added the same id between the check and the insert; its row is kept. Any other failure
            // propagates, so a write that never happened is not mistaken for an existing request.
            return false;
        }
    }

    /// <inheritdoc cref="IOperatorStore.TryUpdateDecisionAsync" />
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

    /// <inheritdoc cref="IOperatorStore.TryClaimDecisionAuditAsync" />
    public async Task<bool> TryClaimDecisionAuditAsync(string approvalId, DateTimeOffset now, DateTimeOffset claimUntil, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var nowTicks = now.UtcTicks;
        var untilTicks = claimUntil.UtcTicks;
        var updated = await context.Approvals
            .Where(row => row.ApprovalId == approvalId && !row.DecisionAudited && (row.AuditClaimedUntilTicks == null || row.AuditClaimedUntilTicks <= nowTicks))
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AuditClaimedUntilTicks, untilTicks), cancellationToken)
            .ConfigureAwait(false);
        return updated == 1;
    }

    /// <inheritdoc cref="IOperatorStore.ReleaseDecisionAuditClaimAsync" />
    public async Task ReleaseDecisionAuditClaimAsync(string approvalId, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Approvals
            .Where(row => row.ApprovalId == approvalId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AuditClaimedUntilTicks, (long?)null), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc cref="IOperatorStore.MarkDecisionAuditedAsync" />
    public async Task MarkDecisionAuditedAsync(string approvalId, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Approvals
            .Where(row => row.ApprovalId == approvalId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.DecisionAudited, true)
                    .SetProperty(row => row.AuditClaimedUntilTicks, (long?)null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc cref="IOperatorStore.ListPendingAsync" />
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
        return rows.Select(ApprovalRows.ToStored).ToArray();
    }

    private bool ApprovalExists(string approvalId)
    {
        using var context = this.contexts.CreateDbContext();
        return context.Approvals.Any(row => row.ApprovalId == approvalId);
    }
}

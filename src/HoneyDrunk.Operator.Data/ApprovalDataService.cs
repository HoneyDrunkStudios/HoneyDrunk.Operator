using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Abstractions.Approvals;
using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>
/// Reads and writes approval requests and their decisions, each write with its outbox entry in one transaction. Each
/// call uses its own context from the factory.
/// </summary>
/// <param name="contexts">Creates a context per call.</param>
/// <param name="timeProvider">The clock checked for each write attempt; defaults to the system clock.</param>
public sealed class ApprovalDataService(IDbContextFactory<OperatorDbContext> contexts, TimeProvider timeProvider)
{
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly IDbContextFactory<OperatorDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <summary>Initializes a new instance of the <see cref="ApprovalDataService"/> class with the system clock.</summary>
    /// <param name="contexts">Creates a context per call.</param>
    public ApprovalDataService(IDbContextFactory<OperatorDbContext> contexts)
        : this(contexts, TimeProvider.System)
    {
    }

    /// <inheritdoc cref="IOperatorStore.GetApprovalAsync" />
    public async Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await context.Approvals.AsNoTracking().SingleOrDefaultAsync(approval => approval.ApprovalId == approvalId, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ApprovalRows.ToStored(row);
    }

    /// <inheritdoc cref="IOperatorStore.TryAddApprovalAsync" />
    public async Task<bool> TryAddApprovalAsync(StoredApproval approval, PendingAudit audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(audit);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.Approvals.AnyAsync(row => row.ApprovalId == approval.Request.ApprovalId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        // The request and its entry are kept together, and a lost commit reply is not replayed.
        try
        {
            return await OperatorUnitOfWork.RunAsync(
                context,
                audit.Id,
                async token =>
                {
                    context.Approvals.Add(ApprovalRows.ToRow(approval));
                    context.PendingAudits.Add(PendingAuditRows.ToRow(audit));
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (this.ApprovalExists(approval.Request.ApprovalId))
        {
            // Another host added the same id between the check and the insert; its row is kept. Any other failure
            // propagates, so a write that never happened is not mistaken for an existing request.
            return false;
        }
    }

    /// <inheritdoc cref="IOperatorStore.TryUpdateDecisionAsync" />
    public async Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, PendingAudit audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(audit);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await OperatorUnitOfWork.RunAsync(
                context,
                audit.Id,
                token => this.UpdateDecisionAsync(context, approvalId, expected, decision, audit, token),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ApprovalExpiredDuringWriteException)
        {
            // The transaction has rolled back both the late decision and any audit entry.
            return false;
        }
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
        return [.. rows.Select(ApprovalRows.ToStored)];
    }

    /// <inheritdoc cref="ISubjectScopedOperatorStore.ListPendingAsync" />
    public async Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, PendingApprovalQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Subjects.Count == 0)
        {
            return [];
        }

        var subjects = query.Subjects.ToArray();
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var nowTicks = now.UtcTicks;
        var rows = await context.Approvals.AsNoTracking()
            .Where(row => row.Outcome == (int)ApprovalOutcome.Pending && row.ExpiryTicks > nowTicks)
            .Where(row => subjects.Contains(row.Subject))
            .OrderByDescending(row => row.RequestedAtTicks)
            .ThenBy(row => row.ApprovalId)
            .Take(query.Limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(ApprovalRows.ToStored)];
    }

    private async Task<bool> UpdateDecisionAsync(OperatorDbContext context, string approvalId, ApprovalOutcome expected, ApprovalDecision decision, PendingAudit audit, CancellationToken cancellationToken)
    {
        var rows = context.Approvals.Where(row => row.ApprovalId == approvalId && row.Outcome == (int)expected);
        var person = decision.Outcome is ApprovalOutcome.Approved or ApprovalOutcome.Denied;
        if (person)
        {
            // This callback runs again on every retry, after the transaction is opened.
            var atTicks = Math.Max(decision.DecidedAt.UtcTicks, this.timeProvider.GetUtcNow().UtcTicks);
            rows = rows.Where(row => row.ExpiryTicks >= atTicks);
        }

        var updated = await rows.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.Outcome, (int)decision.Outcome)
                .SetProperty(row => row.ApproverIdentity, decision.ApproverIdentity)
                .SetProperty(row => row.DecidedAtTicks, decision.DecidedAt.UtcTicks)
                .SetProperty(row => row.Reason, decision.Reason),
            cancellationToken).ConfigureAwait(false);
        if (updated != 1)
        {
            return false;
        }

        context.PendingAudits.Add(PendingAuditRows.ToRow(audit));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (person)
        {
            // A database lock or delayed command can outlive the predicate's timestamp. Check again after both writes,
            // while the transaction still holds the row; throwing rolls back rather than committing a late decision.
            var expiry = await context.Approvals.Where(row => row.ApprovalId == approvalId)
                .Select(row => row.ExpiryTicks).SingleAsync(cancellationToken).ConfigureAwait(false);
            if (this.timeProvider.GetUtcNow().UtcTicks > expiry)
            {
                throw new ApprovalExpiredDuringWriteException();
            }
        }

        return true;
    }

    private bool ApprovalExists(string approvalId)
    {
        using var context = this.contexts.CreateDbContext();
        return context.Approvals.Any(row => row.ApprovalId == approvalId);
    }
}

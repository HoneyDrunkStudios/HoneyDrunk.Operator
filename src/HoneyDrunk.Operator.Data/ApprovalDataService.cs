using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>
/// Reads and writes approval requests and their decisions, each write with its outbox entry in one transaction. Each
/// call uses its own context from the factory.
/// </summary>
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
        return await OperatorUnitOfWork.RunAsync(
            context,
            audit.Id,
            async token =>
            {
                var rows = context.Approvals.Where(row => row.ApprovalId == approvalId && row.Outcome == (int)expected);
                if (decision.Outcome is ApprovalOutcome.Approved or ApprovalOutcome.Denied)
                {
                    // A person's decision dated after the expiry is refused by the same statement that writes it.
                    var decidedAtTicks = decision.DecidedAt.UtcTicks;
                    rows = rows.Where(row => row.ExpiryTicks >= decidedAtTicks);
                }

                var updated = await rows
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(row => row.Outcome, (int)decision.Outcome)
                            .SetProperty(row => row.ApproverIdentity, decision.ApproverIdentity)
                            .SetProperty(row => row.DecidedAtTicks, decision.DecidedAt.UtcTicks)
                            .SetProperty(row => row.Reason, decision.Reason),
                        token)
                    .ConfigureAwait(false);
                if (updated != 1)
                {
                    return false;
                }

                context.PendingAudits.Add(PendingAuditRows.ToRow(audit));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
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

    private bool ApprovalExists(string approvalId)
    {
        using var context = this.contexts.CreateDbContext();
        return context.Approvals.Any(row => row.ApprovalId == approvalId);
    }
}

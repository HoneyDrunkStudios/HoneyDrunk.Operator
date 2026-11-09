using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.State;

/// <summary>
/// Where Operator keeps the state that must survive a restart: approvals and their decisions, manual pauses, spend per
/// budget window, and the outbox of audit entries not yet delivered. The InMemory provider is the default;
/// HoneyDrunk.Operator.Data provides a durable one.
/// </summary>
/// <remarks>
/// Every write that changes state takes the <see cref="PendingAudit"/> recording it and saves both atomically: the
/// change and its entry are kept together or not at all, and a write that changes nothing saves no entry. Entries are
/// then read with <see cref="ClaimAuditsAsync"/> and marked delivered with <see cref="CompleteAuditAsync"/>.
/// </remarks>
public interface IOperatorStore
{
    /// <summary>Reads an approval.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The approval, or null.</returns>
    Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken);

    /// <summary>Adds an approval and its audit entry unless the id already exists.</summary>
    /// <param name="approval">The approval.</param>
    /// <param name="audit">The entry recording the request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>False when the id already exists; the stored one is kept and no entry is saved.</returns>
    Task<bool> TryAddApprovalAsync(StoredApproval approval, PendingAudit audit, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a decision only while the stored outcome is <paramref name="expected"/>, saving its audit entry with it.
    /// An <see cref="ApprovalOutcome.Approved"/> or <see cref="ApprovalOutcome.Denied"/> decision is also refused when
    /// it is dated after the request's expiry, in the same conditional write, so a decision that reaches the store
    /// late can never be recorded on an expired request.
    /// </summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="expected">The outcome the stored decision must still have.</param>
    /// <param name="decision">The new decision.</param>
    /// <param name="audit">The entry recording the decision.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the decision was replaced; false when the outcome differs or the request had expired.</returns>
    Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, PendingAudit audit, CancellationToken cancellationToken);

    /// <summary>Lists pending approvals that have not expired at <paramref name="now"/>, newest first.</summary>
    /// <param name="now">The current time.</param>
    /// <param name="limit">The most to return.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pending approvals.</returns>
    Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Reads a pause.</summary>
    /// <param name="name">The breaker name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pause, or null.</returns>
    Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken);

    /// <summary>Stores a pause, replacing any for the same name, with its audit entry.</summary>
    /// <param name="pause">The pause.</param>
    /// <param name="audit">The entry recording the pause.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    Task SetPauseAsync(PauseState pause, PendingAudit audit, CancellationToken cancellationToken);

    /// <summary>Removes a pause, saving its audit entry only when a pause was removed.</summary>
    /// <param name="name">The breaker name.</param>
    /// <param name="audit">The entry recording the resume.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a pause was removed.</returns>
    Task<bool> RemovePauseAsync(string name, PendingAudit audit, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a charge to its budget window with its audit entry. When the new total reaches the stop's limit and no pause
    /// of that name exists, the stop's pause and audit entries are saved in the same transaction.
    /// </summary>
    /// <param name="charge">The charge.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new total, and whether this charge added the stop's pause.</returns>
    Task<SpendResult> AddSpendAsync(SpendCharge charge, CancellationToken cancellationToken);

    /// <summary>Reads a budget window's total.</summary>
    /// <param name="scope">The budget scope.</param>
    /// <param name="window">The window key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The total, zero when nothing was spent.</returns>
    Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken);

    /// <summary>
    /// Claims up to <paramref name="limit"/> outbox entries for delivery, oldest first, until <paramref name="claimUntil"/>.
    /// An entry is claimable when it was never claimed or its claim ended at or before <paramref name="now"/>, so of
    /// several callers on several hosts each entry goes to one. Each claim must be atomic.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <param name="claimUntil">When the claims lapse if the entries are not completed.</param>
    /// <param name="limit">The most to claim.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The entries this call claimed.</returns>
    Task<IReadOnlyList<PendingAudit>> ClaimAuditsAsync(DateTimeOffset now, DateTimeOffset claimUntil, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Marks an entry delivered, so it is never claimed again. A store that checks for an entry to learn whether a
    /// commit happened must keep the delivered entry long enough for that check, and may remove it after.
    /// </summary>
    /// <param name="auditId">The entry id.</param>
    /// <param name="deliveredAt">When it was delivered.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    Task CompleteAuditAsync(Guid auditId, DateTimeOffset deliveredAt, CancellationToken cancellationToken);
}

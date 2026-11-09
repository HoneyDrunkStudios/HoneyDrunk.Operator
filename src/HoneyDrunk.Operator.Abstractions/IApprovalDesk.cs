namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// Where a person decides held actions: record Approved or Denied on a pending approval, and list what is waiting.
/// A recorded decision is never overwritten.
/// </summary>
public interface IApprovalDesk
{
    /// <summary>Records a person's decision on a pending approval.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="outcome"><see cref="ApprovalOutcome.Approved"/> or <see cref="ApprovalOutcome.Denied"/>.</param>
    /// <param name="approver">Who decided.</param>
    /// <param name="reason">Why, in a short sentence.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>
    /// The stored decision; <see cref="ApprovalOutcome.Expired"/> when the request expired first; or null for an unknown id.
    /// Repeating the same decision returns it unchanged.
    /// </returns>
    /// <exception cref="InvalidOperationException">A different decision is already recorded.</exception>
    Task<ApprovalDecision?> DecideAsync(string approvalId, ApprovalOutcome outcome, string approver, string reason, CancellationToken cancellationToken = default);

    /// <summary>Lists approvals still waiting for a decision, newest first.</summary>
    /// <param name="limit">The most to return, 1 to 100.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pending approvals.</returns>
    Task<IReadOnlyList<PendingApproval>> ListPendingAsync(int limit = 100, CancellationToken cancellationToken = default);
}

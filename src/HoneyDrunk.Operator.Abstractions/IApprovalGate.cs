namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// Requires human approval before a constrained agent action proceeds.
/// </summary>
/// <remarks>
/// The gate is asynchronous and non-blocking: <see cref="RequestAsync"/> raises an approval request
/// (emitted out-of-band as an approval-needed event per ADR-0018 D8) and returns immediately with a
/// <see cref="ApprovalOutcome.Pending"/> decision. Consumers learn the final outcome by polling
/// <see cref="CheckStatusAsync"/>; the emitted event signals only that approval is needed — there is
/// no approval-completed event contract in v0.1.0.
/// </remarks>
public interface IApprovalGate
{
    /// <summary>
    /// Raises an approval request and returns immediately with a pending decision.
    /// </summary>
    /// <param name="request">The approval request describing the subject, action, and context.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A decision that is <see cref="ApprovalOutcome.Pending"/> until a human responds.</returns>
    Task<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current decision for a previously raised approval request.
    /// </summary>
    /// <param name="approvalId">The identifier of the approval request.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The current decision, or <see langword="null"/> when no request matches the identifier.</returns>
    Task<ApprovalDecision?> CheckStatusAsync(string approvalId, CancellationToken cancellationToken = default);
}

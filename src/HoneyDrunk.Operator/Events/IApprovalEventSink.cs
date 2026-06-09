using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.Events;

/// <summary>
/// Transport-agnostic sink for approval-needed events.
/// </summary>
/// <remarks>
/// Per ADR-0018 D8, Operator emits approval-needed events rather than calling Communications
/// directly. The host wires a concrete sink (for example a transport publisher); the default
/// composition uses <see cref="NullApprovalEventSink"/>. The wire shape is deferred to a follow-up
/// per ADR-0018 D8.
/// </remarks>
public interface IApprovalEventSink
{
    /// <summary>
    /// Emits an approval-needed event for the given request.
    /// </summary>
    /// <param name="request">The approval request that needs human attention.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the emission.</returns>
    Task EmitApprovalNeededAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
}

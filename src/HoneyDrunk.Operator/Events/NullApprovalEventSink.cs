using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.Events;

/// <summary>
/// Default no-op <see cref="IApprovalEventSink"/> used when the host has not wired a transport.
/// </summary>
public sealed class NullApprovalEventSink : IApprovalEventSink
{
    /// <inheritdoc />
    public Task EmitApprovalNeededAsync(ApprovalRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

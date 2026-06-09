using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Telemetry;

namespace HoneyDrunk.Operator.Events;

/// <summary>
/// Emits approval-needed events via the configured <see cref="IApprovalEventSink"/> per ADR-0018 D8.
/// </summary>
/// <remarks>
/// Operator does not take a runtime dependency on HoneyDrunk.Communications (invariant 118).
/// Communications subscribes to the emitted event and owns the downstream recipient, preference,
/// cadence, and delivery workflow.
/// </remarks>
/// <param name="sink">The transport-agnostic event sink.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
public sealed class ApprovalEventEmitter(IApprovalEventSink sink, OperatorTelemetry telemetry)
{
    private readonly IApprovalEventSink sink = sink ?? throw new ArgumentNullException(nameof(sink));
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    /// <summary>
    /// Emits an approval-needed event for the given request.
    /// </summary>
    /// <param name="request">The approval request that needs human attention.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the emission.</returns>
    public async Task EmitAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = this.telemetry.Start("approval-gate", "emit-event");
        await this.sink.EmitApprovalNeededAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

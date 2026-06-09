using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Events;
using HoneyDrunk.Operator.Telemetry;
using System.Collections.Concurrent;

namespace HoneyDrunk.Operator.Approval;

/// <summary>
/// Default <see cref="IApprovalGate"/> that records a pending approval, emits an approval-needed
/// event (event-out per ADR-0018 D8), and returns immediately. Consumers poll
/// <see cref="CheckStatusAsync"/> or subscribe to the emitted event for the final decision.
/// </summary>
/// <remarks>
/// Durable persistence of approval state (via HoneyDrunk.Data's repository surface per ADR-0018 D12)
/// is deferred to a follow-up packet; v0.1.0 holds in-flight approvals in process.
/// </remarks>
/// <param name="emitter">The approval event emitter.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
/// <param name="audit">The Operator audit writer.</param>
public sealed class DefaultApprovalGate(
    ApprovalEventEmitter emitter,
    OperatorTelemetry telemetry,
    OperatorAuditWriter audit) : IApprovalGate
{
    private readonly ApprovalEventEmitter emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly OperatorAuditWriter audit = audit ?? throw new ArgumentNullException(nameof(audit));

    // TODO(data): persist pending approvals via HoneyDrunk.Data's IRepository per ADR-0018 D12.
    private readonly ConcurrentDictionary<string, ApprovalDecision> decisions = new(StringComparer.Ordinal);

    // Expiry is carried on the request, not the decision, so a pending decision can be aged out
    // to Expired on read. Held alongside the decision until durable persistence lands.
    private readonly ConcurrentDictionary<string, DateTimeOffset> expiries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = this.telemetry.Start("approval-gate", "request");

        var pending = new ApprovalDecision(request.ApprovalId, ApprovalOutcome.Pending, string.Empty, DateTimeOffset.UtcNow, null);
        this.decisions[request.ApprovalId] = pending;
        this.expiries[request.ApprovalId] = request.Expiry;

        await this.emitter.EmitAsync(request, cancellationToken).ConfigureAwait(false);
        await this.audit.WriteAsync(
            "operator.approval-gate.requested",
            request.Subject,
            AuditOutcome.Pending,
            new AuditTarget("approval", request.ApprovalId),
            request.Action,
            request.RequesterCorrelationId,
            cancellationToken).ConfigureAwait(false);

        return pending;
    }

    /// <inheritdoc />
    public Task<ApprovalDecision?> CheckStatusAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        using var activity = this.telemetry.Start("approval-gate", "check-status");
        if (!this.decisions.TryGetValue(approvalId, out var decision))
        {
            return Task.FromResult<ApprovalDecision?>(null);
        }

        // A still-pending request whose expiry has passed is reported as Expired, so a caller
        // polling the gate can never observe Pending indefinitely. Persist the transition once and
        // drop the expiry entry so it isn't recomputed on every later poll.
        if (decision.Outcome == ApprovalOutcome.Pending
            && this.expiries.TryGetValue(approvalId, out var expiry)
            && DateTimeOffset.UtcNow > expiry)
        {
            decision = decision with { Outcome = ApprovalOutcome.Expired, Reason = "Approval request expired before a decision was recorded." };
            this.decisions[approvalId] = decision;
            this.expiries.TryRemove(approvalId, out _);
        }

        return Task.FromResult<ApprovalDecision?>(decision);
    }
}

using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Events;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Telemetry;
using System.Collections.Frozen;

namespace HoneyDrunk.Operator.Approval;

/// <summary>
/// The default <see cref="IApprovalGate"/>. Requests and decisions live in the <see cref="IOperatorStore"/>, so they
/// survive a restart when the store is durable. A new request never overwrites a decision already recorded, and a
/// pending request past its expiry is reported, once and for all, as <see cref="ApprovalOutcome.Expired"/>.
/// </summary>
/// <param name="emitter">Emits the approval-requested event.</param>
/// <param name="telemetry">Operator telemetry.</param>
/// <param name="outbox">Delivers the saved audit entries.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="store">Where requests and decisions are kept.</param>
public sealed class DefaultApprovalGate(
    ApprovalEventEmitter emitter,
    OperatorTelemetry telemetry,
    OperatorAuditOutbox outbox,
    TimeProvider timeProvider,
    IOperatorStore store) : IApprovalGate
{
    private readonly ApprovalEventEmitter emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly OperatorAuditOutbox outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly IOperatorStore store = store ?? throw new ArgumentNullException(nameof(store));

    /// <inheritdoc />
    public async Task<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ApprovalId);
        ArgumentNullException.ThrowIfNull(request.Context, nameof(request));

        // One read-only snapshot, taken before any await, is what is stored, compared and announced. The caller's
        // dictionary can change afterwards without changing the request a person is asked to decide.
        request = request with { Context = request.Context.ToFrozenDictionary(StringComparer.Ordinal) };
        using var activity = this.telemetry.Start("approval-gate", "request");
        var now = this.timeProvider.GetUtcNow();
        var pending = new ApprovalDecision(request.ApprovalId, ApprovalOutcome.Pending, string.Empty, now, null);
        var audit = PendingAudit.Create(
            "operator.approval-gate.requested",
            request.Subject,
            AuditOutcome.Pending,
            new AuditTarget("approval", request.ApprovalId),
            request.Action,
            request.RequesterCorrelationId,
            now);
        if (!await this.store.TryAddApprovalAsync(new StoredApproval(request, pending, now), audit, cancellationToken).ConfigureAwait(false))
        {
            // The id is known. Only the same request may read its recorded decision; an id reused for a different
            // action, subject, scope or context must never inherit another request's approval.
            var stored = await this.store.GetApprovalAsync(request.ApprovalId, cancellationToken).ConfigureAwait(false);
            if (stored is null || !SameRequest(stored.Request, request))
            {
                throw new InvalidOperationException($"Approval id '{request.ApprovalId}' is already used for a different request.");
            }

            var current = (await this.CheckStatusAsync(request.ApprovalId, cancellationToken).ConfigureAwait(false))!;
            if (current.Outcome == ApprovalOutcome.Pending)
            {
                // Announce again while still pending, so an announcement that failed or was cancelled is not final.
                // The stored request is the one a decision applies to, so that is the one announced.
                await this.emitter.EmitAsync(stored.Request with { Context = stored.Request.Context.ToFrozenDictionary(StringComparer.Ordinal) }, cancellationToken).ConfigureAwait(false);
            }

            return current;
        }

        // The request and its audit entry are saved together; delivery ignores the caller's cancellation.
        await this.outbox.DeliverAsync(CancellationToken.None).ConfigureAwait(false);
        await this.emitter.EmitAsync(request, cancellationToken).ConfigureAwait(false);
        return pending;
    }

    /// <inheritdoc />
    public async Task<ApprovalDecision?> CheckStatusAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        using var activity = this.telemetry.Start("approval-gate", "check-status");
        return await ApprovalExpiry.CurrentAsync(this.store, approvalId, this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    // The incoming context is the gate's own ordinal snapshot, so its lookups are exact whatever comparer the
    // caller's dictionary used. Equal counts and every stored pair found make the two sets of pairs the same.
    private static bool SameRequest(ApprovalRequest stored, ApprovalRequest incoming) =>
        string.Equals(stored.Subject, incoming.Subject, StringComparison.Ordinal)
        && string.Equals(stored.Action, incoming.Action, StringComparison.Ordinal)
        && string.Equals(stored.RequestedScope, incoming.RequestedScope, StringComparison.Ordinal)
        && stored.Context.Count == incoming.Context.Count
        && stored.Context.All(pair => incoming.Context.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.Ordinal));
}

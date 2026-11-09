using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Telemetry;

namespace HoneyDrunk.Operator.Approval;

/// <summary>
/// The default <see cref="IApprovalDesk"/>. A person's decision is written with compare-and-set on the pending state,
/// together with its audit entry, so of two people deciding at once exactly one decision and one entry are stored. The
/// entry is delivered from the store's outbox.
/// </summary>
/// <param name="store">Where requests and decisions are kept.</param>
/// <param name="outbox">Delivers the saved audit entries.</param>
/// <param name="telemetry">Operator telemetry.</param>
/// <param name="timeProvider">The clock.</param>
public sealed class DefaultApprovalDesk(
    IOperatorStore store,
    OperatorAuditOutbox outbox,
    OperatorTelemetry telemetry,
    TimeProvider timeProvider) : IApprovalDesk
{
    /// <summary>The most pending approvals returned by one call.</summary>
    public const int MaxPendingPage = 100;

    private readonly IOperatorStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly OperatorAuditOutbox outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async Task<ApprovalDecision?> DecideAsync(string approvalId, ApprovalOutcome outcome, string approver, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        if (outcome is not (ApprovalOutcome.Approved or ApprovalOutcome.Denied))
        {
            throw new ArgumentException("A person may record only Approved or Denied.", nameof(outcome));
        }

        OperatorText.Identity(approver, nameof(approver));
        OperatorText.Reason(reason, nameof(reason));
        using var activity = this.telemetry.Start("approval-desk", "decide");
        var current = await ApprovalExpiry.CurrentAsync(this.store, approvalId, this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (current is null || current.Outcome != ApprovalOutcome.Pending)
        {
            return Settled(current, outcome);
        }

        // The decision is dated after the read, and the store refuses it once that time is past the request's
        // expiry. A read that stalls cannot turn into an approval of an expired request.
        var now = this.timeProvider.GetUtcNow();
        var decision = new ApprovalDecision(approvalId, outcome, approver, now, reason);
        var audit = PendingAudit.Create(
            "operator.approval-desk.decided",
            approver,
            outcome == ApprovalOutcome.Approved ? AuditOutcome.Succeeded : AuditOutcome.Denied,
            new AuditTarget("approval", approvalId),
            reason,
            null,
            now);
        if (!await this.store.TryUpdateDecisionAsync(approvalId, ApprovalOutcome.Pending, decision, audit, cancellationToken).ConfigureAwait(false))
        {
            // Another decision landed first, or the request expired before this one was dated.
            var stored = await ApprovalExpiry.CurrentAsync(this.store, approvalId, this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            return Settled(stored, outcome);
        }

        await this.outbox.DeliverAsync(CancellationToken.None).ConfigureAwait(false);
        return decision;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingApproval>> ListPendingAsync(int limit = MaxPendingPage, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxPendingPage);
        using var activity = this.telemetry.Start("approval-desk", "list-pending");
        var pending = await this.store.ListPendingAsync(this.timeProvider.GetUtcNow(), limit, cancellationToken).ConfigureAwait(false);
        return [.. pending.Select(item => new PendingApproval(item.Request, item.RequestedAt))];
    }

    // An already-settled approval: the same decision again is a no-op, Expired is reported, anything else conflicts.
    private static ApprovalDecision? Settled(ApprovalDecision? stored, ApprovalOutcome requested) =>
        stored is null || stored.Outcome == requested || stored.Outcome == ApprovalOutcome.Expired
            ? stored
            : throw new InvalidOperationException($"Approval '{stored.ApprovalId}' is already {stored.Outcome}.");
}

using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Telemetry;

namespace HoneyDrunk.Operator.Approval;

/// <summary>
/// The default <see cref="IApprovalDesk"/>. A person's decision is written with compare-and-set on the pending
/// state, so of two people deciding at once exactly one decision is stored, and it is audited once.
/// </summary>
/// <param name="store">Where requests and decisions are kept.</param>
/// <param name="audit">The audit writer.</param>
/// <param name="telemetry">Operator telemetry.</param>
/// <param name="timeProvider">The clock.</param>
public sealed class DefaultApprovalDesk(
    IOperatorStore store,
    OperatorAuditWriter audit,
    OperatorTelemetry telemetry,
    TimeProvider timeProvider) : IApprovalDesk
{
    /// <summary>The most pending approvals returned by one call.</summary>
    public const int MaxPendingPage = 100;

    private readonly IOperatorStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly OperatorAuditWriter audit = audit ?? throw new ArgumentNullException(nameof(audit));
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
        var now = this.timeProvider.GetUtcNow();
        var current = await ApprovalExpiry.CurrentAsync(this.store, approvalId, now, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Outcome != ApprovalOutcome.Pending)
        {
            return await this.SettledAsync(current, outcome).ConfigureAwait(false);
        }

        var decision = new ApprovalDecision(approvalId, outcome, approver, now, reason);
        if (!await this.store.TryUpdateDecisionAsync(approvalId, ApprovalOutcome.Pending, decision, cancellationToken).ConfigureAwait(false))
        {
            // Another decision landed first.
            var stored = (await this.store.GetApprovalAsync(approvalId, cancellationToken).ConfigureAwait(false))?.Decision;
            return await this.SettledAsync(stored, outcome).ConfigureAwait(false);
        }

        await this.AuditDecisionAsync(decision).ConfigureAwait(false);
        return decision;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingApproval>> ListPendingAsync(int limit = MaxPendingPage, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxPendingPage);
        using var activity = this.telemetry.Start("approval-desk", "list-pending");
        var pending = await this.store.ListPendingAsync(this.timeProvider.GetUtcNow(), limit, cancellationToken).ConfigureAwait(false);
        return pending.Select(item => new PendingApproval(item.Request, item.RequestedAt)).ToArray();
    }

    // The decision is stored. Its audit entry and the mark that it was written ignore the caller's cancellation; if the
    // audit sink fails, the mark stays unset and the same decision made again writes the entry then.
    private async Task AuditDecisionAsync(ApprovalDecision decision)
    {
        await this.audit.WriteAsync(
            "operator.approval-desk.decided",
            decision.ApproverIdentity,
            decision.Outcome == ApprovalOutcome.Approved ? AuditOutcome.Succeeded : AuditOutcome.Denied,
            new AuditTarget("approval", decision.ApprovalId),
            decision.Reason,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
        await this.store.MarkDecisionAuditedAsync(decision.ApprovalId, CancellationToken.None).ConfigureAwait(false);
    }

    // The same decision again completes an audit entry that failed earlier; it never writes a second one.
    private async Task<ApprovalDecision?> SettledAsync(ApprovalDecision? stored, ApprovalOutcome requested)
    {
        var settled = Settled(stored, requested);
        if (settled is not null && settled.Outcome == requested
            && await this.store.GetApprovalAsync(settled.ApprovalId, CancellationToken.None).ConfigureAwait(false) is { DecisionAudited: false } record)
        {
            await this.AuditDecisionAsync(record.Decision).ConfigureAwait(false);
        }

        return settled;
    }

    // An already-settled approval: the same decision again is a no-op, Expired is reported, anything else conflicts.
    private static ApprovalDecision? Settled(ApprovalDecision? stored, ApprovalOutcome requested) =>
        stored is null || stored.Outcome == requested || stored.Outcome == ApprovalOutcome.Expired
            ? stored
            : throw new InvalidOperationException($"Approval '{stored.ApprovalId}' is already {stored.Outcome}.");
}

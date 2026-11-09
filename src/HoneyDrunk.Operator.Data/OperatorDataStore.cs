using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;

namespace HoneyDrunk.Operator.Data;

/// <summary>
/// The durable <see cref="IOperatorStore"/>, over one data service per use case: approvals, pauses, spend and the audit
/// outbox. Each call uses its own context from the factory, so the store is safe as a singleton. Each change is saved
/// with its outbox entries in one transaction, and compare-and-set and claims are single conditional statements, so
/// they stay atomic across hosts that share the database.
/// </summary>
/// <param name="approvals">Approval requests and decisions.</param>
/// <param name="pauses">Manual pauses.</param>
/// <param name="spend">Spend per budget window.</param>
/// <param name="audits">The audit outbox.</param>
internal sealed class OperatorDataStore(ApprovalDataService approvals, PauseDataService pauses, SpendDataService spend, PendingAuditDataService audits) : IOperatorStore
{
    private readonly ApprovalDataService approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
    private readonly PauseDataService pauses = pauses ?? throw new ArgumentNullException(nameof(pauses));
    private readonly SpendDataService spend = spend ?? throw new ArgumentNullException(nameof(spend));
    private readonly PendingAuditDataService audits = audits ?? throw new ArgumentNullException(nameof(audits));

    /// <inheritdoc />
    public Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken) => this.approvals.GetApprovalAsync(approvalId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryAddApprovalAsync(StoredApproval approval, PendingAudit audit, CancellationToken cancellationToken) =>
        this.approvals.TryAddApprovalAsync(approval, audit, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, PendingAudit audit, CancellationToken cancellationToken) =>
        this.approvals.TryUpdateDecisionAsync(approvalId, expected, decision, audit, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        this.approvals.ListPendingAsync(now, limit, cancellationToken);

    /// <inheritdoc />
    public Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken) => this.pauses.GetPauseAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task SetPauseAsync(PauseState pause, PendingAudit audit, CancellationToken cancellationToken) => this.pauses.SetPauseAsync(pause, audit, cancellationToken);

    /// <inheritdoc />
    public Task<bool> RemovePauseAsync(string name, PendingAudit audit, CancellationToken cancellationToken) => this.pauses.RemovePauseAsync(name, audit, cancellationToken);

    /// <inheritdoc />
    public Task<SpendResult> AddSpendAsync(SpendCharge charge, CancellationToken cancellationToken) => this.spend.AddSpendAsync(charge, cancellationToken);

    /// <inheritdoc />
    public Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken) => this.spend.GetSpendAsync(scope, window, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingAudit>> ClaimAuditsAsync(DateTimeOffset now, DateTimeOffset claimUntil, int limit, CancellationToken cancellationToken) =>
        this.audits.ClaimAuditsAsync(now, claimUntil, limit, cancellationToken);

    /// <inheritdoc />
    public Task CompleteAuditAsync(Guid auditId, DateTimeOffset deliveredAt, CancellationToken cancellationToken) => this.audits.CompleteAuditAsync(auditId, deliveredAt, cancellationToken);
}

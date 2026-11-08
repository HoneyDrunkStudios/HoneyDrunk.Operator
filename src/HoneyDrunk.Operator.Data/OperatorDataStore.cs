using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;

namespace HoneyDrunk.Operator.Data;

/// <summary>
/// The durable <see cref="IOperatorStore"/>, over one data service per use case: approvals, pauses and spend. Each
/// call uses its own context from the factory, so the store is safe as a singleton. Compare-and-set and spend
/// additions are single SQL statements, so they stay atomic across hosts that share the database.
/// </summary>
/// <param name="approvals">Approval requests and decisions.</param>
/// <param name="pauses">Manual pauses.</param>
/// <param name="spend">Spend per budget window.</param>
internal sealed class OperatorDataStore(ApprovalDataService approvals, PauseDataService pauses, SpendDataService spend) : IOperatorStore
{
    private readonly ApprovalDataService approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
    private readonly PauseDataService pauses = pauses ?? throw new ArgumentNullException(nameof(pauses));
    private readonly SpendDataService spend = spend ?? throw new ArgumentNullException(nameof(spend));

    /// <inheritdoc />
    public Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken) => this.approvals.GetApprovalAsync(approvalId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryAddApprovalAsync(StoredApproval approval, CancellationToken cancellationToken) => this.approvals.TryAddApprovalAsync(approval, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, CancellationToken cancellationToken) =>
        this.approvals.TryUpdateDecisionAsync(approvalId, expected, decision, cancellationToken);

    /// <inheritdoc />
    public Task MarkDecisionAuditedAsync(string approvalId, CancellationToken cancellationToken) => this.approvals.MarkDecisionAuditedAsync(approvalId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        this.approvals.ListPendingAsync(now, limit, cancellationToken);

    /// <inheritdoc />
    public Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken) => this.pauses.GetPauseAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task SetPauseAsync(PauseState pause, CancellationToken cancellationToken) => this.pauses.SetPauseAsync(pause, cancellationToken);

    /// <inheritdoc />
    public Task<bool> TryAddPauseAsync(PauseState pause, CancellationToken cancellationToken) => this.pauses.TryAddPauseAsync(pause, cancellationToken);

    /// <inheritdoc />
    public Task<bool> RemovePauseAsync(string name, CancellationToken cancellationToken) => this.pauses.RemovePauseAsync(name, cancellationToken);

    /// <inheritdoc />
    public Task<decimal> AddSpendAsync(string scope, string window, decimal amount, CancellationToken cancellationToken) => this.spend.AddSpendAsync(scope, window, amount, cancellationToken);

    /// <inheritdoc />
    public Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken) => this.spend.GetSpendAsync(scope, window, cancellationToken);
}

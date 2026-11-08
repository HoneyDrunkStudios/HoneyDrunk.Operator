using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.Testing;

/// <summary>
/// In-memory <see cref="IApprovalDesk"/> for tests, over an <see cref="InMemoryApprovalGate"/>. It lists the
/// gate's pending requests and records decisions on them; it does not check expiry or refuse a changed decision.
/// </summary>
/// <param name="gate">The gate whose requests this desk decides.</param>
public sealed class InMemoryApprovalDesk(InMemoryApprovalGate gate) : IApprovalDesk
{
    private readonly InMemoryApprovalGate gate = gate ?? throw new ArgumentNullException(nameof(gate));

    /// <inheritdoc />
    public Task<ApprovalDecision?> DecideAsync(string approvalId, ApprovalOutcome outcome, string approver, string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult(this.gate.Decide(approvalId, outcome, approver, reason));

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingApproval>> ListPendingAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PendingApproval>>([.. this.gate.Pending().Take(limit)]);
}

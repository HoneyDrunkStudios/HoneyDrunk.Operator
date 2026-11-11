using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Abstractions.Approvals;

namespace HoneyDrunk.Operator.Testing;

/// <summary>
/// In-memory <see cref="IApprovalDesk"/> for tests, over an <see cref="InMemoryApprovalGate"/>. It lists the
/// gate's pending requests and records decisions on them; it does not check expiry or refuse a changed decision.
/// </summary>
/// <param name="gate">The gate whose requests this desk decides.</param>
public sealed class InMemoryApprovalDesk(InMemoryApprovalGate gate) : IApprovalDesk, ISubjectScopedApprovalDesk
{
    private readonly InMemoryApprovalGate gate = gate ?? throw new ArgumentNullException(nameof(gate));

    /// <inheritdoc />
    public Task<ApprovalDecision?> DecideAsync(string approvalId, ApprovalOutcome outcome, string approver, string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult(this.gate.Decide(approvalId, outcome, approver, reason));

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingApproval>> ListPendingAsync(int limit = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PendingApproval>>([.. this.gate.Pending().Take(limit)]);

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingApproval>> ListPendingAsync(PendingApprovalQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Subjects.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PendingApproval>>([]);
        }

        var subjects = query.Subjects.ToHashSet(StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<PendingApproval>>(
            [
                .. this.gate.Pending().Where(pending => subjects.Contains(pending.Request.Subject))
                    .OrderByDescending(pending => pending.RequestedAt)
                    .ThenBy(pending => pending.Request.ApprovalId, StringComparer.Ordinal)
                    .Take(query.Limit),
            ]);
    }
}

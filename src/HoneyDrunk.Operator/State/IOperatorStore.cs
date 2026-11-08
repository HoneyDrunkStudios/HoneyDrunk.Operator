using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.State;

/// <summary>
/// Where Operator keeps the state that must survive a restart: approvals and their decisions, manual pauses, and
/// spend per budget window. The InMemory provider is the default; HoneyDrunk.Operator.Data provides a durable one.
/// Implementations must make <see cref="TryAddApprovalAsync"/>, <see cref="TryUpdateDecisionAsync"/> and
/// <see cref="AddSpendAsync"/> atomic.
/// </summary>
public interface IOperatorStore
{
    /// <summary>Reads an approval.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The approval, or null.</returns>
    Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken);

    /// <summary>Adds an approval unless its id already exists.</summary>
    /// <param name="approval">The approval.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>False when the id already exists; the stored one is kept.</returns>
    Task<bool> TryAddApprovalAsync(StoredApproval approval, CancellationToken cancellationToken);

    /// <summary>Replaces a decision only while the stored outcome is <paramref name="expected"/>.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="expected">The outcome the stored decision must still have.</param>
    /// <param name="decision">The new decision.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the decision was replaced.</returns>
    Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, CancellationToken cancellationToken);

    /// <summary>Lists pending approvals that have not expired at <paramref name="now"/>, newest first.</summary>
    /// <param name="now">The current time.</param>
    /// <param name="limit">The most to return.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pending approvals.</returns>
    Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Reads a pause.</summary>
    /// <param name="name">The breaker name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pause, or null.</returns>
    Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken);

    /// <summary>Stores a pause, replacing any for the same name.</summary>
    /// <param name="pause">The pause.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    Task SetPauseAsync(PauseState pause, CancellationToken cancellationToken);

    /// <summary>Removes a pause.</summary>
    /// <param name="name">The breaker name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a pause was removed.</returns>
    Task<bool> RemovePauseAsync(string name, CancellationToken cancellationToken);

    /// <summary>Adds spend to a budget window atomically.</summary>
    /// <param name="scope">The budget scope, an agent id for agents.</param>
    /// <param name="window">The window key.</param>
    /// <param name="amount">The non-negative amount.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The window's new total.</returns>
    Task<decimal> AddSpendAsync(string scope, string window, decimal amount, CancellationToken cancellationToken);

    /// <summary>Reads a budget window's total.</summary>
    /// <param name="scope">The budget scope.</param>
    /// <param name="window">The window key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The total, zero when nothing was spent.</returns>
    Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken);
}

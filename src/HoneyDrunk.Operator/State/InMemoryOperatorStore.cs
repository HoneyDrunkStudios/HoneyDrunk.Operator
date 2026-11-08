using HoneyDrunk.Operator.Abstractions;
using System.Collections.Concurrent;

namespace HoneyDrunk.Operator.State;

/// <summary>The default store: process memory only, so nothing survives a restart. Use HoneyDrunk.Operator.Data in production.</summary>
public sealed class InMemoryOperatorStore : IOperatorStore
{
    private readonly ConcurrentDictionary<string, StoredApproval> approvals = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PauseState> pauses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string scope, string window), decimal> spend = new();

    /// <inheritdoc />
    public Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken) =>
        Task.FromResult(this.approvals.GetValueOrDefault(approvalId));

    /// <inheritdoc />
    public Task<bool> TryAddApprovalAsync(StoredApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        var copy = approval with { Request = approval.Request with { Context = new Dictionary<string, string>(approval.Request.Context, StringComparer.Ordinal) } };
        return Task.FromResult(this.approvals.TryAdd(approval.Request.ApprovalId, copy));
    }

    /// <inheritdoc />
    public Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        while (this.approvals.TryGetValue(approvalId, out var current))
        {
            if (current.Decision.Outcome != expected)
            {
                return Task.FromResult(false);
            }

            if (this.approvals.TryUpdate(approvalId, current with { Decision = decision }, current))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredApproval> pending = this.approvals.Values
            .Where(approval => approval.Decision.Outcome == ApprovalOutcome.Pending && approval.Request.Expiry > now)
            .OrderByDescending(approval => approval.RequestedAt)
            .ThenBy(approval => approval.Request.ApprovalId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
        return Task.FromResult(pending);
    }

    /// <inheritdoc />
    public Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(this.pauses.GetValueOrDefault(name));

    /// <inheritdoc />
    public Task SetPauseAsync(PauseState pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        this.pauses[pause.Name] = pause;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> TryAddPauseAsync(PauseState pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        return Task.FromResult(this.pauses.TryAdd(pause.Name, pause));
    }

    /// <inheritdoc />
    public Task<bool> RemovePauseAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(this.pauses.TryRemove(name, out _));

    /// <inheritdoc />
    public Task<decimal> AddSpendAsync(string scope, string window, decimal amount, CancellationToken cancellationToken) =>
        Task.FromResult(this.spend.AddOrUpdate((scope, window), amount, (_, existing) => existing + amount));

    /// <inheritdoc />
    public Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken) =>
        Task.FromResult(this.spend.GetValueOrDefault((scope, window)));
}

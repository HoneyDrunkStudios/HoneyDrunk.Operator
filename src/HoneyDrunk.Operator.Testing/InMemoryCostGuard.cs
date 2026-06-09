using HoneyDrunk.Operator.Abstractions;
using System.Collections.Concurrent;

namespace HoneyDrunk.Operator.Testing;

/// <summary>
/// In-memory <see cref="ICostGuard"/> for tests. Accumulates spend with a caller-supplied per-scope
/// budget (<see cref="DefaultLimit"/>, or <c>0</c> for unlimited).
/// </summary>
public sealed class InMemoryCostGuard : ICostGuard
{
    private readonly ConcurrentDictionary<string, decimal> spend = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the per-scope budget applied to every window (<c>0</c> means unlimited).</summary>
    public decimal DefaultLimit { get; set; }

    /// <inheritdoc />
    public Task<CostCheckResult> CheckBudgetAsync(string scope, string window, decimal amount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(window);
        var current = this.spend.GetValueOrDefault(Key(scope, window));
        var projected = current + amount;
        if (this.DefaultLimit > 0m && projected > this.DefaultLimit)
        {
            return Task.FromResult(new CostCheckResult(false, this.DefaultLimit - current, this.DefaultLimit, "Budget exceeded."));
        }

        return Task.FromResult(new CostCheckResult(true, this.DefaultLimit > 0m ? this.DefaultLimit - projected : decimal.MaxValue, this.DefaultLimit, null));
    }

    /// <inheritdoc />
    public Task RecordAsync(CostEvent costEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(costEvent);
        this.spend.AddOrUpdate(Key(costEvent.AgentId, costEvent.Window), costEvent.Amount, (_, existing) => existing + costEvent.Amount);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<CostStatus> GetStatusAsync(string scope, string window, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(window);
        var current = this.spend.GetValueOrDefault(Key(scope, window));
        return Task.FromResult(new CostStatus(current, this.DefaultLimit, this.DefaultLimit > 0m ? this.DefaultLimit - current : decimal.MaxValue, window));
    }

    private static string Key(string scope, string window) => $"{scope}::{window}";
}

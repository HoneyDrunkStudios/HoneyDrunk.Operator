using HoneyDrunk.Operator.Abstractions;
using System.Collections.Concurrent;

namespace HoneyDrunk.Operator.Testing;

/// <summary>
/// In-memory <see cref="ICircuitBreaker"/> for tests. Defaults to closed; trips and resets are
/// honored so tests can simulate breaker behavior deterministically.
/// </summary>
public sealed class InMemoryCircuitBreaker : ICircuitBreaker
{
    private readonly ConcurrentDictionary<string, BreakerState> states = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<bool> IsAllowedAsync(string breakerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        return Task.FromResult(this.states.GetValueOrDefault(breakerName, BreakerState.Closed) != BreakerState.Open);
    }

    /// <inheritdoc />
    public Task TripAsync(string breakerName, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        this.states[breakerName] = BreakerState.Open;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetAsync(string breakerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        this.states[breakerName] = BreakerState.Closed;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BreakerState> GetStateAsync(string breakerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        return Task.FromResult(this.states.GetValueOrDefault(breakerName, BreakerState.Closed));
    }
}

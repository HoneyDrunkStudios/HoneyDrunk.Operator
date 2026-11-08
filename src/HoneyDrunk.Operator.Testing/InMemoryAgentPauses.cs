using HoneyDrunk.Operator.Abstractions;
using System.Collections.Concurrent;

namespace HoneyDrunk.Operator.Testing;

/// <summary>In-memory <see cref="IAgentPauses"/> for tests. Pausing again keeps the first pause.</summary>
/// <param name="timeProvider">
/// Optional clock used to stamp pauses (Grid clock policy); defaults to <see cref="TimeProvider.System"/>.
/// </param>
public sealed class InMemoryAgentPauses(TimeProvider? timeProvider = null) : IAgentPauses
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, PauseState> pauses = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<PauseState> PauseAsync(string name, string pausedBy, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.FromResult(this.pauses.GetOrAdd(name, key => new PauseState(key, pausedBy, reason, this.timeProvider.GetUtcNow())));
    }

    /// <inheritdoc />
    public Task<bool> ResumeAsync(string name, string resumedBy, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.FromResult(this.pauses.TryRemove(name, out _));
    }

    /// <inheritdoc />
    public Task<PauseState?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.FromResult(this.pauses.TryGetValue(name, out var state) ? state : null);
    }
}

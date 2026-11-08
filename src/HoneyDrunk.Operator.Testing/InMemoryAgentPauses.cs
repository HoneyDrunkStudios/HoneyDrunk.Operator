using HoneyDrunk.Operator.Abstractions;
using System.Collections.Concurrent;

namespace HoneyDrunk.Operator.Testing;

/// <summary>In-memory <see cref="IAgentPauses"/> for tests. Pausing again keeps when the pause began and replaces who and why.</summary>
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
        var now = this.timeProvider.GetUtcNow();
        return Task.FromResult(this.pauses.AddOrUpdate(name, key => new PauseState(key, pausedBy, reason, now), (_, existing) => existing with { PausedBy = pausedBy, Reason = reason }));
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

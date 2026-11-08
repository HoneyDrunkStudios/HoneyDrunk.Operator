namespace HoneyDrunk.Operator.Abstractions;

/// <summary>A manual pause: who, why and since when.</summary>
/// <param name="Name">The breaker name, <c>agent:{agentId}</c> for agents.</param>
/// <param name="PausedBy">Who paused it.</param>
/// <param name="Reason">Why.</param>
/// <param name="Since">When the pause began.</param>
public sealed record PauseState(string Name, string PausedBy, string Reason, DateTimeOffset Since);

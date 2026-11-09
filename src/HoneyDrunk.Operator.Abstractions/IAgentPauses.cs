namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// Manual pauses, keyed by breaker name (<c>agent:{agentId}</c> for agents). A paused name is refused by the circuit
/// breaker until a person resumes it; the breaker's own reset never lifts a pause.
/// </summary>
public interface IAgentPauses
{
    /// <summary>Pauses a name. Pausing again keeps the first time and replaces who and why.</summary>
    /// <param name="name">The breaker name.</param>
    /// <param name="pausedBy">Who paused it.</param>
    /// <param name="reason">Why, in a short sentence.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stored pause.</returns>
    Task<PauseState> PauseAsync(string name, string pausedBy, string reason, CancellationToken cancellationToken = default);

    /// <summary>Lifts a pause.</summary>
    /// <param name="name">The breaker name.</param>
    /// <param name="resumedBy">Who lifted it.</param>
    /// <param name="reason">Why, in a short sentence.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a pause was lifted; false when the name was not paused.</returns>
    Task<bool> ResumeAsync(string name, string resumedBy, string reason, CancellationToken cancellationToken = default);

    /// <summary>Reads a name's pause.</summary>
    /// <param name="name">The breaker name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pause, or null when the name is not paused.</returns>
    Task<PauseState?> GetAsync(string name, CancellationToken cancellationToken = default);
}

namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// Halts agent or workflow execution when safety thresholds are breached.
/// </summary>
/// <remarks>
/// Circuit breakers are named so a single Operator composition can guard many independent paths.
/// Thresholds (failure count, half-open trial count, reset window) are sourced from Azure App
/// Configuration via Vault's configuration provider per ADR-0018 D6 — never hardcoded.
/// </remarks>
public interface ICircuitBreaker
{
    /// <summary>
    /// Indicates whether calls are currently permitted through the named breaker.
    /// </summary>
    /// <param name="breakerName">The logical breaker name.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the breaker is closed or half-open and permits a trial call.</returns>
    Task<bool> IsAllowedAsync(string breakerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces the named breaker open, blocking calls until the reset window elapses.
    /// </summary>
    /// <param name="breakerName">The logical breaker name.</param>
    /// <param name="reason">A human-readable reason recorded for audit and diagnostics.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the trip operation.</returns>
    Task TripAsync(string breakerName, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces the named breaker closed.
    /// </summary>
    /// <param name="breakerName">The logical breaker name.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the reset operation.</returns>
    Task ResetAsync(string breakerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current state of the named breaker.
    /// </summary>
    /// <param name="breakerName">The logical breaker name.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The current <see cref="BreakerState"/>.</returns>
    Task<BreakerState> GetStateAsync(string breakerName, CancellationToken cancellationToken = default);
}

/// <summary>
/// The lifecycle state of a circuit breaker.
/// </summary>
public enum BreakerState
{
    /// <summary>Calls are permitted.</summary>
    Closed = 0,

    /// <summary>Calls are blocked until the reset window elapses.</summary>
    Open = 1,

    /// <summary>A limited number of trial calls are permitted to probe recovery.</summary>
    HalfOpen = 2,
}

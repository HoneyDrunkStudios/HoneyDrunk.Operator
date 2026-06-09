namespace HoneyDrunk.Operator.Abstractions;

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

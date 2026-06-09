namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// The accumulated spend status for a scope and window.
/// </summary>
/// <param name="Spent">The amount spent so far in the window.</param>
/// <param name="Limit">The configured budget limit.</param>
/// <param name="Remaining">The remaining budget.</param>
/// <param name="Window">The budget window the status describes.</param>
public sealed record CostStatus(decimal Spent, decimal Limit, decimal Remaining, string Window);

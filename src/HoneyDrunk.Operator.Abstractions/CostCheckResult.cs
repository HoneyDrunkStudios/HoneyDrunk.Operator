namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// The outcome of a budget check.
/// </summary>
/// <param name="Allowed">Whether the proposed spend is permitted.</param>
/// <param name="Remaining">The remaining budget after the proposed spend, when allowed.</param>
/// <param name="Limit">The configured budget limit for the scope and window.</param>
/// <param name="DenyReason">The reason the spend was denied, or <see langword="null"/> when allowed.</param>
public sealed record CostCheckResult(bool Allowed, decimal Remaining, decimal Limit, string? DenyReason);

namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// The outcome of a decision-policy evaluation.
/// </summary>
/// <param name="Outcome">The policy outcome.</param>
/// <param name="Reason">An optional human-readable reason.</param>
/// <param name="EvaluatedRules">The identifiers of the rules that were evaluated.</param>
public sealed record PolicyDecision(PolicyOutcome Outcome, string? Reason, string[] EvaluatedRules);

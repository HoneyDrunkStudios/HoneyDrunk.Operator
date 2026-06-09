namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// The outcome of a safety-filter evaluation.
/// </summary>
/// <param name="Allowed">Whether the content is permitted.</param>
/// <param name="BlockReason">The reason the content was blocked, or <see langword="null"/> when allowed.</param>
/// <param name="FiredRules">The identifiers of the rules that fired.</param>
public sealed record SafetyFilterResult(bool Allowed, string? BlockReason, string[] FiredRules);

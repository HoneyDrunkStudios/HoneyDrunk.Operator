using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.Safety;

/// <summary>
/// A single composable safety rule evaluated by <see cref="DefaultSafetyFilter"/>.
/// </summary>
/// <remarks>
/// Consumers register custom rules through dependency injection; the default filter evaluates the
/// full registered chain and aggregates the fired rules.
/// </remarks>
public interface ISafetyRule
{
    /// <summary>Gets the stable rule identifier reported in <see cref="SafetyFilterResult.FiredRules"/>.</summary>
    string RuleId { get; }

    /// <summary>
    /// Evaluates the request against this rule.
    /// </summary>
    /// <param name="request">The content and context to evaluate.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the rule fires (content blocked by this rule).</returns>
    Task<bool> IsViolatedAsync(SafetyFilterRequest request, CancellationToken cancellationToken = default);
}

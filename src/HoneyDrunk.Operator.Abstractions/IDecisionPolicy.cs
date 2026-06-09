namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// Evaluates whether an action is allowed, denied, or requires human approval.
/// </summary>
/// <remarks>
/// Decision-policy rule sets are sourced from Azure App Configuration via Vault's configuration
/// provider per ADR-0018 D6. The default runtime implementation delegates authorization to
/// HoneyDrunk.Auth per ADR-0018 D5.
/// </remarks>
public interface IDecisionPolicy
{
    /// <summary>
    /// Evaluates an action against the active policy rule set.
    /// </summary>
    /// <param name="context">The action context to evaluate.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The policy decision and the rules that were evaluated.</returns>
    Task<PolicyDecision> EvaluateAsync(ActionContext context, CancellationToken cancellationToken = default);
}

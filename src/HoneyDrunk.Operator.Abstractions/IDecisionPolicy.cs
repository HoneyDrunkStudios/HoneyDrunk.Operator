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

/// <summary>
/// Describes an action being evaluated by a decision policy.
/// </summary>
/// <param name="ActorId">The identity performing the action.</param>
/// <param name="Action">The action being attempted.</param>
/// <param name="Resource">The resource the action targets.</param>
/// <param name="Attributes">Additional attributes available to policy rules.</param>
public sealed record ActionContext(string ActorId, string Action, string Resource, IReadOnlyDictionary<string, string> Attributes);

/// <summary>
/// The outcome of a decision-policy evaluation.
/// </summary>
/// <param name="Outcome">The policy outcome.</param>
/// <param name="Reason">An optional human-readable reason.</param>
/// <param name="EvaluatedRules">The identifiers of the rules that were evaluated.</param>
public sealed record PolicyDecision(PolicyOutcome Outcome, string? Reason, string[] EvaluatedRules);

/// <summary>
/// The possible outcomes of a decision-policy evaluation.
/// </summary>
public enum PolicyOutcome
{
    /// <summary>The action is permitted.</summary>
    Allow = 0,

    /// <summary>The action is denied.</summary>
    Deny = 1,

    /// <summary>The action requires human approval before it can proceed.</summary>
    RequireApproval = 2,
}

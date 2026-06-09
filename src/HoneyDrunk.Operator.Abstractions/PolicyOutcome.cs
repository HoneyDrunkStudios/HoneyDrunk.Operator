namespace HoneyDrunk.Operator.Abstractions;

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

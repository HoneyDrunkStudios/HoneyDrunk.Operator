namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// Describes an action being evaluated by a decision policy.
/// </summary>
/// <param name="ActorId">The identity performing the action.</param>
/// <param name="Action">The action being attempted.</param>
/// <param name="Resource">The resource the action targets.</param>
/// <param name="Attributes">Additional attributes available to policy rules.</param>
public sealed record ActionContext(string ActorId, string Action, string Resource, IReadOnlyDictionary<string, string> Attributes);

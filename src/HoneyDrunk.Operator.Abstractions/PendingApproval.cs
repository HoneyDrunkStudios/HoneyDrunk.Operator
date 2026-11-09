namespace HoneyDrunk.Operator.Abstractions;

/// <summary>An approval still waiting for a person's decision.</summary>
/// <param name="Request">The request as it was raised.</param>
/// <param name="RequestedAt">When it was raised.</param>
public sealed record PendingApproval(ApprovalRequest Request, DateTimeOffset RequestedAt);

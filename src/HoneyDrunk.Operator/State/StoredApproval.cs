using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.State;

/// <summary>An approval request with its current decision.</summary>
/// <param name="Request">The request as raised.</param>
/// <param name="Decision">The current decision; <see cref="ApprovalOutcome.Pending"/> until decided or expired.</param>
/// <param name="RequestedAt">When it was raised.</param>
public sealed record StoredApproval(ApprovalRequest Request, ApprovalDecision Decision, DateTimeOffset RequestedAt);

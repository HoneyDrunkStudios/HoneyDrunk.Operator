using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.State;

/// <summary>An approval request with its current decision.</summary>
/// <param name="Request">The request as raised.</param>
/// <param name="Decision">The current decision; <see cref="ApprovalOutcome.Pending"/> until decided or expired.</param>
/// <param name="RequestedAt">When it was raised.</param>
/// <param name="DecisionAudited">Whether the recorded decision's audit entry has been written.</param>
/// <param name="AuditClaimedUntil">When the current claim on writing that entry lapses; null when none is held.</param>
public sealed record StoredApproval(ApprovalRequest Request, ApprovalDecision Decision, DateTimeOffset RequestedAt, bool DecisionAudited = false, DateTimeOffset? AuditClaimedUntil = null);

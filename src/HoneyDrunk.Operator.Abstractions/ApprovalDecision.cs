namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// The decision recorded against an <see cref="ApprovalRequest"/>.
/// </summary>
/// <param name="ApprovalId">The identifier of the approval request this decision answers.</param>
/// <param name="Outcome">The decision outcome.</param>
/// <param name="ApproverIdentity">The identity that decided, or an empty string while pending.</param>
/// <param name="DecidedAt">The instant the decision was recorded.</param>
/// <param name="Reason">An optional human-readable reason.</param>
public sealed record ApprovalDecision(
    string ApprovalId,
    ApprovalOutcome Outcome,
    string ApproverIdentity,
    DateTimeOffset DecidedAt,
    string? Reason);

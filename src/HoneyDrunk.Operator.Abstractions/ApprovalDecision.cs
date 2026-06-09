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

/// <summary>
/// The possible outcomes of an approval request.
/// </summary>
public enum ApprovalOutcome
{
    /// <summary>The request is awaiting a human decision.</summary>
    Pending = 0,

    /// <summary>The request was approved.</summary>
    Approved = 1,

    /// <summary>The request was denied.</summary>
    Denied = 2,

    /// <summary>The request expired before a decision was made.</summary>
    Expired = 3,
}

namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// A request for human approval of a constrained action.
/// </summary>
/// <param name="ApprovalId">The stable identifier for this approval request.</param>
/// <param name="Subject">The identity or entity the action concerns.</param>
/// <param name="Action">The action awaiting approval.</param>
/// <param name="Context">Additional attributes describing the action.</param>
/// <param name="RequestedScope">The scope the approval grants when approved.</param>
/// <param name="Expiry">The instant after which the request expires if undecided.</param>
/// <param name="RequesterCorrelationId">The correlation identifier of the requesting operation. Plain string by design — Abstractions carries no HoneyDrunk identity types per invariant 1.</param>
public sealed record ApprovalRequest(
    string ApprovalId,
    string Subject,
    string Action,
    IReadOnlyDictionary<string, string> Context,
    string RequestedScope,
    DateTimeOffset Expiry,
    string RequesterCorrelationId);

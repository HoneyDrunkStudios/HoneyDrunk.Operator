using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using System.Text.Json;

namespace HoneyDrunk.Operator.Data;

/// <summary>Maps approvals to and from their rows. Times are stored as UTC ticks.</summary>
internal static class ApprovalRows
{
    internal static DateTimeOffset FromTicks(long ticks) => new(ticks, TimeSpan.Zero);

    internal static ApprovalEntity ToRow(StoredApproval approval) => new()
    {
        ApprovalId = approval.Request.ApprovalId,
        Subject = approval.Request.Subject,
        Action = approval.Request.Action,
        ContextJson = JsonSerializer.Serialize(approval.Request.Context),
        RequestedScope = approval.Request.RequestedScope,
        ExpiryTicks = approval.Request.Expiry.UtcTicks,
        RequesterCorrelationId = approval.Request.RequesterCorrelationId,
        RequestedAtTicks = approval.RequestedAt.UtcTicks,
        Outcome = (int)approval.Decision.Outcome,
        ApproverIdentity = approval.Decision.ApproverIdentity,
        DecidedAtTicks = approval.Decision.DecidedAt.UtcTicks,
        Reason = approval.Decision.Reason,
        DecisionAudited = approval.DecisionAudited,
        AuditClaimedUntilTicks = approval.AuditClaimedUntil?.UtcTicks,
    };

    internal static StoredApproval ToStored(ApprovalEntity row)
    {
        var context = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ContextJson) ?? [];
        var request = new ApprovalRequest(row.ApprovalId, row.Subject, row.Action, context, row.RequestedScope, FromTicks(row.ExpiryTicks), row.RequesterCorrelationId);
        var decision = new ApprovalDecision(row.ApprovalId, (ApprovalOutcome)row.Outcome, row.ApproverIdentity, FromTicks(row.DecidedAtTicks), row.Reason);
        var claimedUntil = row.AuditClaimedUntilTicks is { } ticks ? FromTicks(ticks) : (DateTimeOffset?)null;
        return new StoredApproval(request, decision, FromTicks(row.RequestedAtTicks), row.DecisionAudited, claimedUntil);
    }
}

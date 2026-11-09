using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.State;

namespace HoneyDrunk.Operator.Data;

/// <summary>Maps outbox entries to and from their rows. Times are stored as UTC ticks.</summary>
internal static class PendingAuditRows
{
    internal static PendingAuditEntity ToRow(PendingAudit audit) => new()
    {
        Id = audit.Id,
        EventName = audit.EventName,
        Actor = audit.Actor,
        Outcome = (int)audit.Outcome,
        TargetType = audit.TargetType,
        TargetId = audit.TargetId,
        Reason = audit.Reason,
        CorrelationId = audit.CorrelationId,
        CreatedAtTicks = audit.CreatedAt.UtcTicks,
    };

    internal static PendingAudit ToAudit(PendingAuditEntity row) =>
        new(row.Id, row.EventName, row.Actor, (AuditOutcome)row.Outcome, row.TargetType, row.TargetId, row.Reason, row.CorrelationId, new DateTimeOffset(row.CreatedAtTicks, TimeSpan.Zero));
}

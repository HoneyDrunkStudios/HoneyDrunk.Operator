using HoneyDrunk.Audit.Abstractions;

namespace HoneyDrunk.Operator.State;

/// <summary>
/// An audit entry in the store's outbox. It is saved in the same transaction as the change it records, so the change
/// and its evidence are kept or lost together, and it is delivered to the audit sinks afterwards, at least once.
/// </summary>
/// <param name="Id">The entry id. <see cref="Create"/> orders ids by creation time.</param>
/// <param name="EventName">The audited event, for example <c>operator.pauses.resumed</c>.</param>
/// <param name="Actor">Who made the change.</param>
/// <param name="Outcome">The outcome recorded.</param>
/// <param name="TargetType">The kind of thing changed, for example <c>pause</c>.</param>
/// <param name="TargetId">The id of the thing changed.</param>
/// <param name="Reason">Why, when given.</param>
/// <param name="CorrelationId">The caller's correlation id, when given.</param>
/// <param name="CreatedAt">When the change was made.</param>
public sealed record PendingAudit(
    Guid Id,
    string EventName,
    string Actor,
    AuditOutcome Outcome,
    string TargetType,
    string TargetId,
    string? Reason,
    string? CorrelationId,
    DateTimeOffset CreatedAt)
{
    /// <summary>The longest reason kept; longer text is cut to fit the outbox column.</summary>
    public const int MaxReasonLength = 2000;

    /// <summary>The longest correlation id kept; a longer one is cut to fit the outbox column.</summary>
    public const int MaxCorrelationIdLength = 200;

    /// <summary>Creates an entry with a new id ordered by <paramref name="createdAt"/>.</summary>
    /// <param name="eventName">The audited event.</param>
    /// <param name="actor">Who made the change.</param>
    /// <param name="outcome">The outcome recorded.</param>
    /// <param name="target">The thing changed.</param>
    /// <param name="reason">Why, when given.</param>
    /// <param name="correlationId">The caller's correlation id, when given.</param>
    /// <param name="createdAt">When the change was made.</param>
    /// <returns>The entry.</returns>
    public static PendingAudit Create(string eventName, string actor, AuditOutcome outcome, AuditTarget target, string? reason, string? correlationId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new(Guid.CreateVersion7(createdAt), eventName, actor, outcome, target.Type, target.Id, Fit(reason, MaxReasonLength), Fit(correlationId, MaxCorrelationIdLength), createdAt);
    }

    // Free text from callers is cut, never refused: the change it records matters more than its full wording.
    private static string? Fit(string? text, int maxLength) => text is { Length: > 0 } && text.Length > maxLength ? text[..maxLength] : text;
}

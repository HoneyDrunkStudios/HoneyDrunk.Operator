namespace HoneyDrunk.Operator.Data;

/// <summary>An audit entry saved in the same transaction as the change it records, then delivered from the outbox.</summary>
public sealed class PendingAuditEntity
{
    /// <summary>Gets or sets the insertion order, assigned by the database; entries are delivered in this order.</summary>
    public long Sequence { get; set; }

    /// <summary>Gets or sets the entry id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the audited event.</summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>Gets or sets who made the change.</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>Gets or sets the outcome recorded.</summary>
    public int Outcome { get; set; }

    /// <summary>Gets or sets the kind of thing changed.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Gets or sets the id of the thing changed.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Gets or sets why, when given.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets the caller's correlation id, when given.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets when the change was made, in UTC ticks.</summary>
    public long CreatedAtTicks { get; set; }

    /// <summary>Gets or sets when the current delivery claim lapses, in UTC ticks; null when unclaimed.</summary>
    public long? ClaimedUntilTicks { get; set; }

    /// <summary>
    /// Gets or sets when the entry was delivered, in UTC ticks; null while it waits. A delivered entry is kept for
    /// <see cref="PendingAuditDataService.DeliveredRetention"/> as the proof that its change committed.
    /// </summary>
    public long? DeliveredAtTicks { get; set; }
}

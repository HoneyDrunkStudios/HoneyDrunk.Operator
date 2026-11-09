namespace HoneyDrunk.Operator.Data;

/// <summary>An approval request with its current decision.</summary>
public sealed class ApprovalEntity
{
    /// <summary>Gets or sets the approval id.</summary>
    public string ApprovalId { get; set; } = string.Empty;

    /// <summary>Gets or sets the subject that asked.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the action asked for.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the request context as JSON.</summary>
    public string ContextJson { get; set; } = "{}";

    /// <summary>Gets or sets the scope asked for.</summary>
    public string RequestedScope { get; set; } = string.Empty;

    /// <summary>Gets or sets when the request expires, in UTC ticks.</summary>
    public long ExpiryTicks { get; set; }

    /// <summary>Gets or sets the requester's correlation id.</summary>
    public string RequesterCorrelationId { get; set; } = string.Empty;

    /// <summary>Gets or sets when the request was raised, in UTC ticks.</summary>
    public long RequestedAtTicks { get; set; }

    /// <summary>Gets or sets the decision outcome.</summary>
    public int Outcome { get; set; }

    /// <summary>Gets or sets who decided.</summary>
    public string ApproverIdentity { get; set; } = string.Empty;

    /// <summary>Gets or sets when the decision was made, in UTC ticks.</summary>
    public long DecidedAtTicks { get; set; }

    /// <summary>Gets or sets why.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets a value indicating whether the recorded decision's audit entry has been written.</summary>
    public bool DecisionAudited { get; set; }

    /// <summary>Gets or sets when the current claim on writing the decision's audit entry lapses, in UTC ticks; null when none is held.</summary>
    public long? AuditClaimedUntilTicks { get; set; }
}

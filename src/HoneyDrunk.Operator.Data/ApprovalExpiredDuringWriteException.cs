namespace HoneyDrunk.Operator.Data;

/// <summary>Rolls back a decision whose database write outlived the approval window.</summary>
public sealed class ApprovalExpiredDuringWriteException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ApprovalExpiredDuringWriteException"/> class.</summary>
    public ApprovalExpiredDuringWriteException()
        : base("The approval expired while its decision was being written.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApprovalExpiredDuringWriteException"/> class with a message.</summary>
    /// <param name="message">The failure description.</param>
    public ApprovalExpiredDuringWriteException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ApprovalExpiredDuringWriteException"/> class with a message and cause.</summary>
    /// <param name="message">The failure description.</param>
    /// <param name="innerException">The underlying failure.</param>
    public ApprovalExpiredDuringWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

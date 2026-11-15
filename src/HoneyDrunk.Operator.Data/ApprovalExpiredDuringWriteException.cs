namespace HoneyDrunk.Operator.Data;

/// <summary>Rolls back a decision whose database write outlived the approval window.</summary>
internal sealed class ApprovalExpiredDuringWriteException : Exception
{
    public ApprovalExpiredDuringWriteException()
        : base("The approval expired while its decision was being written.")
    {
    }

    public ApprovalExpiredDuringWriteException(string message)
        : base(message)
    {
    }

    public ApprovalExpiredDuringWriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace HoneyDrunk.Operator.Abstractions;

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

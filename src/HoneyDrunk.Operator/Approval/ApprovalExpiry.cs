using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;

namespace HoneyDrunk.Operator.Approval;

/// <summary>Reads an approval's current decision, recording Expired once when a pending request has run out of time.</summary>
internal static class ApprovalExpiry
{
    /// <summary>The reason recorded on an expired request.</summary>
    public const string ExpiredReason = "Approval request expired before a decision was recorded.";

    /// <summary>Reads the current decision.</summary>
    /// <param name="store">The store.</param>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The decision, or null for an unknown id.</returns>
    public static async Task<ApprovalDecision?> CurrentAsync(IOperatorStore store, string approvalId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stored = await store.GetApprovalAsync(approvalId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return null;
        }

        if (stored.Decision.Outcome != ApprovalOutcome.Pending || now <= stored.Request.Expiry)
        {
            return stored.Decision;
        }

        var expired = stored.Decision with { Outcome = ApprovalOutcome.Expired, DecidedAt = now, Reason = ExpiredReason };
        if (await store.TryUpdateDecisionAsync(approvalId, ApprovalOutcome.Pending, expired, cancellationToken).ConfigureAwait(false))
        {
            return expired;
        }

        // Someone decided or expired it first; report what was stored.
        return (await store.GetApprovalAsync(approvalId, cancellationToken).ConfigureAwait(false))?.Decision;
    }
}

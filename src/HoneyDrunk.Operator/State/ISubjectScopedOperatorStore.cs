using HoneyDrunk.Operator.Abstractions.Approvals;

namespace HoneyDrunk.Operator.State;

/// <summary>An optional store capability that applies a pending-approval subject filter before limiting records.</summary>
public interface ISubjectScopedOperatorStore
{
    /// <summary>Reads unexpired pending approvals for the selected subjects, newest first, bounded before materialization.</summary>
    /// <param name="now">The current time.</param>
    /// <param name="query">The subjects and maximum result count. Empty subjects match nothing.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The matching records, ordered by request time descending and approval id ascending.</returns>
    Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, PendingApprovalQuery query, CancellationToken cancellationToken);
}

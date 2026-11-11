namespace HoneyDrunk.Operator.Abstractions.Approvals;

/// <summary>An optional read capability for desks that can filter subjects before applying the result limit.</summary>
public interface ISubjectScopedApprovalDesk
{
    /// <summary>Lists pending approvals for the requested subjects, newest first, with the subject filter applied before the result limit.</summary>
    /// <param name="query">The subjects and maximum result count. Empty subjects match nothing.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The matching pending approvals, ordered by request time descending and approval id ascending.</returns>
    Task<IReadOnlyList<PendingApproval>> ListPendingAsync(PendingApprovalQuery query, CancellationToken cancellationToken = default);
}

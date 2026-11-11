namespace HoneyDrunk.Operator.Abstractions.Approvals;

/// <summary>An immutable, bounded subject filter applied before ordering and limiting pending approvals.</summary>
public sealed class PendingApprovalQuery
{
    /// <summary>The maximum number of subjects in one query.</summary>
    public const int MaxSubjects = 100;

    /// <summary>The maximum number of approvals returned by one query.</summary>
    public const int MaxLimit = 100;

    /// <summary>Initializes a new instance of the <see cref="PendingApprovalQuery"/> class from a private subject copy.</summary>
    /// <param name="subjects">The subject identities, compared using the store's identity semantics.</param>
    /// <param name="limit">The most approvals to return, from 1 to 100.</param>
    public PendingApprovalQuery(IReadOnlyCollection<string> subjects, int limit = MaxLimit)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxLimit);
        var snapshot = subjects.Take(MaxSubjects + 1).ToArray();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(snapshot.Length, MaxSubjects, nameof(subjects));
        foreach (var subject in snapshot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(subject.Length, 200);
        }

        this.Subjects = Array.AsReadOnly(snapshot.Distinct(StringComparer.Ordinal).ToArray());
        this.Limit = limit;
    }

    /// <summary>Gets the subject identities to include. No subjects means no results, never all subjects.</summary>
    public IReadOnlyList<string> Subjects { get; }

    /// <summary>Gets the maximum number of results.</summary>
    public int Limit { get; }
}

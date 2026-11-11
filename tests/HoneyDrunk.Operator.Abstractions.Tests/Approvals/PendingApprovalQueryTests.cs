using HoneyDrunk.Operator.Abstractions.Approvals;
using Xunit;

namespace HoneyDrunk.Operator.Abstractions.Tests.Approvals;

/// <summary>The subject query is bounded and cannot change after construction.</summary>
public sealed class PendingApprovalQueryTests
{
    /// <summary>Subjects are copied and deduplicated exactly; an empty scope remains empty.</summary>
    [Fact]
    public void Subjects_are_copied_without_expanding_scope()
    {
        string[] subjects = ["agent:a", "agent:a", "agent:A"];
        var query = new PendingApprovalQuery(subjects, 2);
        subjects[0] = "user:b";

        Assert.Equal(["agent:a", "agent:A"], query.Subjects);
        Assert.Equal(2, query.Limit);
        Assert.Empty(new PendingApprovalQuery([]).Subjects);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)query.Subjects)[0] = "user:b");
    }

    /// <summary>The values validated are the same private snapshot that the query exposes.</summary>
    [Fact]
    public void Caller_collection_is_enumerated_only_once()
    {
        var subjects = new ChangingSubjects();

        var query = new PendingApprovalQuery(subjects);

        Assert.Equal(["agent:a"], query.Subjects);
        Assert.Equal(1, subjects.Enumerations);
    }

    /// <summary>A caller-supplied count cannot bypass the actual enumerated-subject bound.</summary>
    [Fact]
    public void Oversized_enumeration_stops_after_the_bounded_probe()
    {
        var subjects = new ChangingSubjects(102);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingApprovalQuery(subjects));
        Assert.Equal(PendingApprovalQuery.MaxSubjects + 1, subjects.Yielded);
        Assert.Equal(1, subjects.Enumerations);
    }

    /// <summary>Inputs cannot enlarge the maximum subject set or page size.</summary>
    [Fact]
    public void Invalid_or_oversized_queries_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PendingApprovalQuery(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingApprovalQuery(new string[101]));
        Assert.Throws<ArgumentException>(() => new PendingApprovalQuery([" "]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingApprovalQuery([new string('a', 201)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingApprovalQuery(["a"], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PendingApprovalQuery(["a"], 101));
    }
}

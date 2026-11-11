using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Abstractions.Approvals;
using Xunit;

namespace HoneyDrunk.Operator.Testing.Tests.Approvals;

/// <summary>The testing desk implements the same optional subject-scoped read surface.</summary>
public sealed class SubjectScopedDeskTests
{
    /// <summary>Newer unrelated requests cannot displace matching subjects from a limited page.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Scope_precedes_limit()
    {
        var gate = new InMemoryApprovalGate { DefaultOutcome = ApprovalOutcome.Pending };
        var desk = new InMemoryApprovalDesk(gate);
        await gate.RequestAsync(Request("wanted", "agent:a"));
        for (var number = 0; number < 101; number++)
        {
            await gate.RequestAsync(Request($"noise-{number}", "user:a"));
        }

        var result = await desk.ListPendingAsync(new PendingApprovalQuery(["agent:a"], 1));

        Assert.Equal("wanted", Assert.Single(result).Request.ApprovalId);
        Assert.Empty(await desk.ListPendingAsync(new PendingApprovalQuery([])));
    }

    private static ApprovalRequest Request(string id, string subject) =>
        new(id, subject, "tool.publish", new Dictionary<string, string>(), "tool:any@1", new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), "correlation-1");
}

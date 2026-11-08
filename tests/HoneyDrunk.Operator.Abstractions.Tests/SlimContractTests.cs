using Xunit;

namespace HoneyDrunk.Operator.Abstractions.Tests;

/// <summary>The 0.2.0 contracts: pauses, pending approvals and cost windows.</summary>
public sealed class SlimContractTests
{
    private static readonly DateTimeOffset SampleInstant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The budget window is the UTC year and month.</summary>
    [Fact]
    public void CostWindows_month_is_utc_year_month()
    {
        Assert.Equal("2026-12", CostWindows.Month(new DateTimeOffset(2026, 11, 30, 23, 30, 0, TimeSpan.FromHours(-5))));
        Assert.Equal("2026-01", CostWindows.Month(SampleInstant));
    }

    /// <summary>The new records construct with their members.</summary>
    [Fact]
    public void PauseState_and_PendingApproval_construct()
    {
        var pause = new PauseState("agent:a", "founder", "checking", SampleInstant);
        var request = new ApprovalRequest("a1", "subject", "tool.x", new Dictionary<string, string>(), "scope", SampleInstant, "corr");
        var pending = new PendingApproval(request, SampleInstant);

        Assert.Equal("founder", pause.PausedBy);
        Assert.Same(request, pending.Request);
    }
}

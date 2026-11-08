using HoneyDrunk.Operator.Abstractions;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>An agent is paused when its monthly budget is spent.</summary>
public sealed class BudgetStopTests
{
    private const string AgentId = "agent-a";
    private const string Window = "2026-11";

    private static readonly Dictionary<string, string> TenDollarLimit = new(StringComparer.Ordinal)
    {
        [$"HoneyDrunk:Operator:Budget:{AgentId}:{Window}"] = "10",
    };

    /// <summary>Spend reaching the limit pauses the agent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Spend_reaching_the_limit_pauses_the_agent()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);

        await fixture.CostGuard.RecordAsync(Spend(6m));
        Assert.Null(await fixture.Pauses.GetAsync("agent:" + AgentId));
        await fixture.CostGuard.RecordAsync(Spend(4m));

        var pause = await fixture.Pauses.GetAsync("agent:" + AgentId);
        Assert.Equal("operator:budget", pause!.PausedBy);
        Assert.Equal("monthly budget reached", pause.Reason);
        Assert.False(await fixture.Breaker.IsAllowedAsync("agent:" + AgentId));
    }

    /// <summary>Spend below the limit leaves the agent running.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Spend_below_the_limit_does_not_pause()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);

        await fixture.CostGuard.RecordAsync(Spend(9.99m));

        Assert.Null(await fixture.Pauses.GetAsync("agent:" + AgentId));
    }

    /// <summary>A zero limit still means unlimited.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Zero_limit_means_no_stop()
    {
        var fixture = new OperatorFixture();

        await fixture.CostGuard.RecordAsync(Spend(1000m));

        Assert.Null(await fixture.Pauses.GetAsync("agent:" + AgentId));
    }

    /// <summary>Spend after the stop does not pause again or audit again.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Later_spend_does_not_duplicate_the_pause_or_audit()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);
        await fixture.CostGuard.RecordAsync(Spend(10m));
        var first = await fixture.Pauses.GetAsync("agent:" + AgentId);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        await fixture.CostGuard.RecordAsync(Spend(2m));

        Assert.Equal(first, await fixture.Pauses.GetAsync("agent:" + AgentId));
        Assert.Single(fixture.AuditLog.Entries, entry => entry.EventName == "operator.cost-guard.budget-stop");
        Assert.Single(fixture.AuditLog.Entries, entry => entry.EventName == "operator.pauses.paused");
    }

    /// <summary>A new month does not lift the stop by itself.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task New_month_does_not_resume_automatically()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);
        await fixture.CostGuard.RecordAsync(Spend(10m));

        fixture.Clock.Advance(TimeSpan.FromDays(31));

        Assert.False(await fixture.Breaker.IsAllowedAsync("agent:" + AgentId));
    }

    /// <summary>A new cost guard over the same store sees the recorded spend.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Check_budget_uses_stored_spend()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);
        await fixture.CostGuard.RecordAsync(Spend(7m));

        var restarted = new OperatorFixture(fixture.Store, TenDollarLimit);
        var check = await restarted.CostGuard.CheckBudgetAsync(AgentId, Window, 4m);

        Assert.False(check.Allowed);
        Assert.Equal(3m, check.Remaining);
    }

    private static CostEvent Spend(decimal amount) =>
        new(Guid.NewGuid().ToString("N"), AgentId, "tenant", Window, amount, "usd", "model", new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero), "corr");
}

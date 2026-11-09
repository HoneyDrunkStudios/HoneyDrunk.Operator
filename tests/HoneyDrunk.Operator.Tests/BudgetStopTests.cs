using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Vault.Abstractions;
using NSubstitute;
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

    /// <summary>A failing audit sink never skips the stop or fails the charge; the entries are delivered later.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Stop_survives_a_failing_audit_sink()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);
        fixture.AuditLog.ThrowOn = "operator.cost-guard.recorded";

        await fixture.CostGuard.RecordAsync(Spend(10m));
        Assert.NotNull(await fixture.Pauses.GetAsync("agent:" + AgentId));
        Assert.Equal(["operator.pauses.paused", "operator.cost-guard.budget-stop"], fixture.AuditLog.Entries.Select(entry => entry.EventName));

        fixture.AuditLog.ThrowOn = null;
        fixture.Clock.Advance(OperatorAuditOutbox.ClaimLease);
        await fixture.Outbox.DeliverAsync();

        Assert.Equal(
            ["operator.pauses.paused", "operator.cost-guard.budget-stop", "operator.cost-guard.recorded"],
            fixture.AuditLog.Entries.Select(entry => entry.EventName));
    }

    /// <summary>Once the spend is stored, the stop and its audit do not depend on the caller's token.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Stop_and_audit_do_not_use_the_callers_token()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);
        using var cancellation = new CancellationTokenSource();

        await fixture.CostGuard.RecordAsync(Spend(10m), cancellation.Token);

        Assert.NotEmpty(fixture.AuditLog.Tokens);
        Assert.All(fixture.AuditLog.Tokens, token => Assert.False(token.canBeCanceled, token.eventName));
    }

    /// <summary>A budget set for the agent without a month applies in every month.</summary>
    /// <param name="window">The month the spend lands in.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("2026-11")]
    [InlineData("2026-12")]
    public async Task Agent_budget_applies_in_every_month(string window)
    {
        var fixture = new OperatorFixture(config: new Dictionary<string, string>(StringComparer.Ordinal) { [$"HoneyDrunk:Operator:Budget:{AgentId}"] = "10" });

        await fixture.CostGuard.RecordAsync(Spend(10m, window));

        Assert.NotNull(await fixture.Pauses.GetAsync("agent:" + AgentId));
        Assert.False((await fixture.CostGuard.CheckBudgetAsync(AgentId, window, 1m)).Allowed);
    }

    /// <summary>A budget for one month wins over the agent's standing budget.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Month_budget_overrides_agent_budget()
    {
        var fixture = new OperatorFixture(config: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"HoneyDrunk:Operator:Budget:{AgentId}"] = "100",
            [$"HoneyDrunk:Operator:Budget:{AgentId}:{Window}"] = "10",
        });

        await fixture.CostGuard.RecordAsync(Spend(10m));

        Assert.NotNull(await fixture.Pauses.GetAsync("agent:" + AgentId));
    }

    /// <summary>A person's pause is never overwritten by the budget stop, and no stop is recorded.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Budget_stop_never_overwrites_a_person_pause()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);
        await fixture.Pauses.PauseAsync("agent:" + AgentId, "founder", "checking");

        await fixture.CostGuard.RecordAsync(Spend(10m));

        var pause = await fixture.Pauses.GetAsync("agent:" + AgentId);
        Assert.Equal("founder", pause!.PausedBy);
        Assert.Equal("checking", pause.Reason);
        Assert.DoesNotContain(fixture.AuditLog.Entries, entry => entry.EventName == "operator.cost-guard.budget-stop");
    }

    /// <summary>An agent id too long for its pause name is refused before any spend is stored.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Agent_id_too_long_for_a_pause_is_refused()
    {
        var fixture = new OperatorFixture(config: TenDollarLimit);
        var longId = new string('a', 195);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.CostGuard.RecordAsync(Spend(1m) with { AgentId = longId }));

        Assert.Equal(0m, await fixture.Store.GetSpendAsync(longId, Window, CancellationToken.None));
    }

    /// <summary>A charge is not stored when its budget limit cannot be read, so no spend is left without its stop.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Charge_is_not_stored_when_the_limit_cannot_be_read()
    {
        var config = Substitute.For<IConfigProvider>();
        config.GetValueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new InvalidOperationException("configuration unavailable"));
        var fixture = new OperatorFixture(configProvider: config);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CostGuard.RecordAsync(Spend(10m)));

        Assert.Equal(0m, await fixture.Store.GetSpendAsync(AgentId, Window, CancellationToken.None));
    }

    private static CostEvent Spend(decimal amount, string window = Window) =>
        new(Guid.NewGuid().ToString("N"), AgentId, "tenant", window, amount, "usd", "model", new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero), "corr");
}

using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Testing;
using Xunit;

namespace HoneyDrunk.Operator.Testing.Tests;

/// <summary>Smoke tests for the in-memory Operator fixtures.</summary>
public sealed class FixtureTests
{
    /// <summary>The in-memory approval gate records requests and returns the configured outcome.</summary>
    [Fact]
    public async Task ApprovalGate_records_and_returns_configured_outcome()
    {
        var gate = new InMemoryApprovalGate { DefaultOutcome = ApprovalOutcome.Denied };
        var request = new ApprovalRequest("a1", "s", "act", new Dictionary<string, string>(), "scope", DateTimeOffset.UtcNow, "corr");

        var decision = await gate.RequestAsync(request);
        Assert.Equal(ApprovalOutcome.Denied, decision.Outcome);
        Assert.Single(gate.ReceivedRequests);
        Assert.Equal(decision.Outcome, (await gate.CheckStatusAsync("a1"))!.Outcome);
    }

    /// <summary>The in-memory breaker honors trip and reset.</summary>
    [Fact]
    public async Task CircuitBreaker_honors_trip_and_reset()
    {
        var breaker = new InMemoryCircuitBreaker();
        Assert.True(await breaker.IsAllowedAsync("svc"));
        await breaker.TripAsync("svc", "reason");
        Assert.False(await breaker.IsAllowedAsync("svc"));
        await breaker.ResetAsync("svc");
        Assert.True(await breaker.IsAllowedAsync("svc"));
    }

    /// <summary>The in-memory cost guard accumulates and enforces the supplied budget.</summary>
    [Fact]
    public async Task CostGuard_enforces_supplied_budget()
    {
        var guard = new InMemoryCostGuard { DefaultLimit = 10m };
        await guard.RecordAsync(new CostEvent("e1", "agent", "t", "daily", 8m, "usd", "model", DateTimeOffset.UtcNow, "corr"));
        var result = await guard.CheckBudgetAsync("agent", "daily", 5m);
        Assert.False(result.Allowed);
    }

    /// <summary>The permissive policy and safety filter always allow.</summary>
    [Fact]
    public async Task Permissive_fixtures_allow()
    {
        var policy = await new PermissiveDecisionPolicy().EvaluateAsync(new ActionContext("a", "act", "r", new Dictionary<string, string>()));
        Assert.Equal(PolicyOutcome.Allow, policy.Outcome);

        var safety = await new PermissiveSafetyFilter().CheckAsync(new SafetyFilterRequest("c", "k", new Dictionary<string, string>()));
        Assert.True(safety.Allowed);
    }
}

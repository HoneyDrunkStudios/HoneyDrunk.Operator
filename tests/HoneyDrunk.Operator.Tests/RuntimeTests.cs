using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Approval;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Breaker;
using HoneyDrunk.Operator.Cost;
using HoneyDrunk.Operator.Events;
using HoneyDrunk.Operator.Policy;
using HoneyDrunk.Operator.Telemetry;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>Unit tests for the HoneyDrunk.Operator default runtime implementations.</summary>
public sealed class RuntimeTests
{
    /// <summary>The breaker walks Closed → Open → (after window) HalfOpen → Closed.</summary>
    [Fact]
    public async Task CircuitBreaker_walks_the_state_machine()
    {
        var config = TestDoubles.ConfigProvider(new Dictionary<string, string>
        {
            ["HoneyDrunk:Operator:Breaker:svc:ResetWindowSeconds"] = "0",
        });
        var breaker = new DefaultCircuitBreaker(config, Options(), Telemetry(), Audit());

        Assert.True(await breaker.IsAllowedAsync("svc"));
        Assert.Equal(BreakerState.Closed, await breaker.GetStateAsync("svc"));

        await breaker.TripAsync("svc", "too many failures");
        Assert.Equal(BreakerState.Open, await breaker.GetStateAsync("svc"));

        // Reset window is 0 seconds, so the next allowed-check transitions to HalfOpen.
        Assert.True(await breaker.IsAllowedAsync("svc"));
        Assert.Equal(BreakerState.HalfOpen, await breaker.GetStateAsync("svc"));

        await breaker.ResetAsync("svc");
        Assert.Equal(BreakerState.Closed, await breaker.GetStateAsync("svc"));
    }

    /// <summary>The cost guard accumulates spend and denies once the configured budget is exceeded.</summary>
    [Fact]
    public async Task CostGuard_accumulates_and_denies_over_budget()
    {
        var config = TestDoubles.ConfigProvider(new Dictionary<string, string>
        {
            ["HoneyDrunk:Operator:Budget:agent:daily"] = "10",
        });
        var guard = new DefaultCostGuard(config, Options(), Telemetry(), Audit());

        var first = await guard.CheckBudgetAsync("agent", "daily", 6m);
        Assert.True(first.Allowed);

        await guard.RecordAsync(new CostEvent("e1", "agent", "tenant", "daily", 6m, "usd", "model", DateTimeOffset.UtcNow, "corr"));
        var status = await guard.GetStatusAsync("agent", "daily");
        Assert.Equal(6m, status.Spent);

        var over = await guard.CheckBudgetAsync("agent", "daily", 5m);
        Assert.False(over.Allowed);
        Assert.NotNull(over.DenyReason);
    }

    /// <summary>The decision policy resolves Allow / Deny / RequireApproval from configuration.</summary>
    /// <param name="configured">The policy value stored in configuration for the action.</param>
    /// <param name="expected">The outcome the policy is expected to resolve.</param>
    [Theory]
    [InlineData("Allow", PolicyOutcome.Allow)]
    [InlineData("Deny", PolicyOutcome.Deny)]
    [InlineData("RequireApproval", PolicyOutcome.RequireApproval)]
    public async Task DecisionPolicy_resolves_outcome_from_config(string configured, PolicyOutcome expected)
    {
        var config = TestDoubles.ConfigProvider(new Dictionary<string, string>
        {
            ["HoneyDrunk:Operator:Policy:deploy"] = configured,
        });
        var policy = new AuthBackedDecisionPolicy(config, Options(), Telemetry());

        var decision = await policy.EvaluateAsync(new ActionContext("actor", "deploy", "resource", new Dictionary<string, string>()));
        Assert.Equal(expected, decision.Outcome);
    }

    /// <summary>Unknown actions fail safe to RequireApproval.</summary>
    [Fact]
    public async Task DecisionPolicy_defaults_unknown_action_to_require_approval()
    {
        var policy = new AuthBackedDecisionPolicy(TestDoubles.ConfigProvider(), Options(), Telemetry());
        var decision = await policy.EvaluateAsync(new ActionContext("actor", "unmapped", "resource", new Dictionary<string, string>()));
        Assert.Equal(PolicyOutcome.RequireApproval, decision.Outcome);
    }

    /// <summary>The approval gate emits an event and returns a pending decision that is queryable.</summary>
    [Fact]
    public async Task ApprovalGate_emits_event_and_tracks_pending()
    {
        var sink = Substitute.For<IApprovalEventSink>();
        var emitter = new ApprovalEventEmitter(sink, Telemetry());
        var gate = new DefaultApprovalGate(emitter, Telemetry(), Audit());

        var request = new ApprovalRequest("a1", "subject", "deploy", new Dictionary<string, string>(), "scope", DateTimeOffset.UtcNow.AddMinutes(5), "corr");
        var decision = await gate.RequestAsync(request);

        Assert.Equal(ApprovalOutcome.Pending, decision.Outcome);
        await sink.Received(1).EmitApprovalNeededAsync(request, Arg.Any<CancellationToken>());

        var status = await gate.CheckStatusAsync("a1");
        Assert.NotNull(status);
        Assert.Equal(ApprovalOutcome.Pending, status!.Outcome);
    }

    /// <summary>The audit writer appends to every registered sink when one is composed.</summary>
    [Fact]
    public async Task AuditWriter_appends_when_sink_present()
    {
        var log = Substitute.For<IAuditLog>();
        var writer = new OperatorAuditWriter([log]);
        Assert.True(writer.IsEnabled);

        await writer.WriteAsync("operator.test", "actor", AuditOutcome.Succeeded, new AuditTarget("kind", "id"));
        await log.Received(1).AppendAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    /// <summary>With no audit sink composed, emission is a no-op rather than a failure.</summary>
    [Fact]
    public async Task AuditWriter_is_noop_when_no_sink()
    {
        var writer = new OperatorAuditWriter(Array.Empty<IAuditLog>());
        Assert.False(writer.IsEnabled);
        await writer.WriteAsync("operator.test", "actor", AuditOutcome.Succeeded, AuditTarget.None);
    }

    private static OperatorTelemetry Telemetry() => new(Substitute.For<ITelemetryActivityFactory>());

    private static OperatorAuditWriter Audit() => new(Array.Empty<IAuditLog>());

    private static IOptions<OperatorOptions> Options(OperatorOptions? options = null) =>
        Microsoft.Extensions.Options.Options.Create(options ?? new OperatorOptions());
}

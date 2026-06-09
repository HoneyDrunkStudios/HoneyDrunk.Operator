using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Safety;
using HoneyDrunk.Operator.Telemetry;
using NSubstitute;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>Tests for the composable safety-filter rule chain.</summary>
public sealed class SafetyFilterTests
{
    /// <summary>With no rules registered the filter is permissive.</summary>
    [Fact]
    public async Task Empty_chain_allows()
    {
        var filter = new DefaultSafetyFilter([], Telemetry());
        var result = await filter.CheckAsync(Request());
        Assert.True(result.Allowed);
        Assert.Empty(result.FiredRules);
    }

    /// <summary>A firing rule blocks the content and is reported.</summary>
    [Fact]
    public async Task Firing_rule_blocks_and_is_reported()
    {
        var filter = new DefaultSafetyFilter([new AlwaysFiresRule()], Telemetry());
        var result = await filter.CheckAsync(Request());
        Assert.False(result.Allowed);
        Assert.Contains("always-fires", result.FiredRules);
    }

    private static OperatorTelemetry Telemetry() => new(Substitute.For<ITelemetryActivityFactory>());

    private static SafetyFilterRequest Request() =>
        new("content", "chat-output", new Dictionary<string, string>());

    private sealed class AlwaysFiresRule : ISafetyRule
    {
        public string RuleId => "always-fires";

        public Task<bool> IsViolatedAsync(SafetyFilterRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}

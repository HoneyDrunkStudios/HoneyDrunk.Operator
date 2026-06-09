using Xunit;

namespace HoneyDrunk.Operator.Abstractions.Tests;

/// <summary>Contract surface smoke tests for the Operator abstractions.</summary>
public sealed class ContractSurfaceTests
{
    // Fixed instant for record-construction smoke tests; the exact value is irrelevant and a literal
    // keeps these off the system clock (Grid clock policy).
    private static readonly DateTimeOffset SampleInstant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Records construct and expose their members.</summary>
    [Fact]
    public void Records_construct_with_expected_members()
    {
        var request = new ApprovalRequest("a1", "subject", "deploy", new Dictionary<string, string>(), "scope", SampleInstant, "corr");
        Assert.Equal("a1", request.ApprovalId);

        var decision = new ApprovalDecision("a1", ApprovalOutcome.Approved, "approver", SampleInstant, "ok");
        Assert.Equal(ApprovalOutcome.Approved, decision.Outcome);

        var cost = new CostEvent("e1", "agent", "tenant", "daily", 1.5m, "usd", "model", SampleInstant, "corr");
        Assert.Equal(1.5m, cost.Amount);

        var check = new CostCheckResult(true, 5m, 10m, null);
        Assert.True(check.Allowed);
    }

    /// <summary>Policy and breaker enums expose the documented members.</summary>
    [Fact]
    public void Enums_expose_documented_members()
    {
        Assert.Equal(0, (int)PolicyOutcome.Allow);
        Assert.Equal(2, (int)PolicyOutcome.RequireApproval);
        Assert.Equal(1, (int)BreakerState.Open);
        Assert.Equal(3, (int)ApprovalOutcome.Expired);
    }
}

using HoneyDrunk.Operator.Abstractions;
using Xunit;

namespace HoneyDrunk.Operator.Abstractions.Tests;

/// <summary>Contract surface smoke tests for the Operator abstractions.</summary>
public sealed class ContractSurfaceTests
{
    /// <summary>Records construct and expose their members.</summary>
    [Fact]
    public void Records_construct_with_expected_members()
    {
        var request = new ApprovalRequest("a1", "subject", "deploy", new Dictionary<string, string>(), "scope", DateTimeOffset.UtcNow, "corr");
        Assert.Equal("a1", request.ApprovalId);

        var decision = new ApprovalDecision("a1", ApprovalOutcome.Approved, "approver", DateTimeOffset.UtcNow, "ok");
        Assert.Equal(ApprovalOutcome.Approved, decision.Outcome);

        var cost = new CostEvent("e1", "agent", "tenant", "daily", 1.5m, "usd", "model", DateTimeOffset.UtcNow, "corr");
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

using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>Tests that <c>AddHoneyDrunkOperator</c> composes the full contract surface.</summary>
public sealed class ServiceRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestDoubles.ConfigProvider());
        services.AddSingleton(Substitute.For<ITelemetryActivityFactory>());
        services.AddHoneyDrunkOperator();
        return services.BuildServiceProvider();
    }

    /// <summary>All five Operator contracts resolve from the container.</summary>
    [Fact]
    public void All_five_contracts_resolve()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetService<IApprovalGate>());
        Assert.NotNull(provider.GetService<ICircuitBreaker>());
        Assert.NotNull(provider.GetService<ICostGuard>());
        Assert.NotNull(provider.GetService<IDecisionPolicy>());
        Assert.NotNull(provider.GetService<ISafetyFilter>());
    }

    /// <summary>The telemetry and audit helpers resolve, and audit degrades to disabled with no sink.</summary>
    [Fact]
    public void Helpers_resolve_and_audit_degrades_gracefully()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetService<OperatorTelemetry>());
        Assert.False(provider.GetRequiredService<Audit.OperatorAuditWriter>().IsEnabled);
    }
}

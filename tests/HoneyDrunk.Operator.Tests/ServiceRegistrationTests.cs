using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>Tests that <c>AddHoneyDrunkOperator</c> composes the full contract surface.</summary>
public sealed class ServiceRegistrationTests
{
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

    /// <summary>The desk, the pauses and the in-memory store resolve, and the whole graph validates on build.</summary>
    [Fact]
    public void Registers_desk_pauses_and_inmemory_store()
    {
        using var provider = BuildProvider();

        Assert.IsType<Approval.DefaultApprovalDesk>(provider.GetRequiredService<IApprovalDesk>());
        Assert.IsType<Pauses.DefaultAgentPauses>(provider.GetRequiredService<IAgentPauses>());
        Assert.IsType<State.InMemoryOperatorStore>(provider.GetRequiredService<State.IOperatorStore>());
    }

    /// <summary>The telemetry and audit helpers resolve, and audit degrades to disabled with no sink.</summary>
    [Fact]
    public void Helpers_resolve_and_audit_degrades_gracefully()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetService<OperatorTelemetry>());
        Assert.False(provider.GetRequiredService<Audit.OperatorAuditWriter>().IsEnabled);
    }

    /// <summary>The audit outbox and its background delivery are registered.</summary>
    [Fact]
    public void Audit_outbox_delivery_is_registered()
    {
        using var provider = BuildProvider();
        Assert.NotNull(provider.GetService<Audit.OperatorAuditOutbox>());
        Assert.Contains(provider.GetServices<IHostedService>(), service => service is Audit.OperatorAuditDeliveryService);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestDoubles.ConfigProvider());
        services.AddSingleton(Substitute.For<ITelemetryActivityFactory>());
        services.AddHoneyDrunkOperator();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}

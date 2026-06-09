using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Approval;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Breaker;
using HoneyDrunk.Operator.Cost;
using HoneyDrunk.Operator.Events;
using HoneyDrunk.Operator.Policy;
using HoneyDrunk.Operator.Safety;
using HoneyDrunk.Operator.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Operator;

/// <summary>
/// Registers HoneyDrunk.Operator runtime services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds HoneyDrunk.Operator's default policy-enforcement services.
    /// </summary>
    /// <remarks>
    /// Registers the five Operator-owned contracts plus the telemetry, audit, and approval event-out
    /// helpers. The host is responsible for composing Kernel telemetry, Vault configuration, and
    /// (optionally) the Audit Node — when no <c>IAuditLog</c> is registered, audit emission is a no-op.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional runtime options configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddHoneyDrunkOperator(this IServiceCollection services, Action<OperatorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OperatorOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Clock policy: runtime components read time via TimeProvider, never DateTimeOffset.UtcNow.
        // TryAdd so a host that composes its own (e.g. a test FakeTimeProvider) wins.
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<OperatorTelemetry>();
        services.AddSingleton<OperatorAuditWriter>();
        services.TryAddApprovalSink();
        services.AddSingleton<ApprovalEventEmitter>();

        services.AddSingleton<IApprovalGate, DefaultApprovalGate>();
        services.AddSingleton<ICircuitBreaker, DefaultCircuitBreaker>();
        services.AddSingleton<ICostGuard, DefaultCostGuard>();
        services.AddSingleton<IDecisionPolicy, AuthBackedDecisionPolicy>();
        services.AddSingleton<ISafetyFilter, DefaultSafetyFilter>();

        return services;
    }

    /// <summary>
    /// Registers a custom safety rule into the <see cref="DefaultSafetyFilter"/> chain.
    /// </summary>
    /// <typeparam name="TRule">The safety rule type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddSafetyRule<TRule>(this IServiceCollection services)
        where TRule : class, ISafetyRule
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISafetyRule, TRule>();
        return services;
    }

    private static void TryAddApprovalSink(this IServiceCollection services)
    {
        if (!services.Any(static d => d.ServiceType == typeof(IApprovalEventSink)))
        {
            services.AddSingleton<IApprovalEventSink, NullApprovalEventSink>();
        }
    }
}

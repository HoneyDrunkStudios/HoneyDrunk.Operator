using HoneyDrunk.Data.EntityFramework.Registration;
using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Operator.Data;

/// <summary>Registers the durable Operator store.</summary>
public static class OperatorDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OperatorDbContext"/> through HoneyDrunk.Data and makes the Data store the
    /// <see cref="IOperatorStore"/>, replacing the in-memory default whether this is called before or after
    /// <c>AddHoneyDrunkOperator</c>. The host creates the <c>operator</c> schema (migrations or <c>EnsureCreated</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDbContext">Configures the database provider, for example <c>UseSqlServer</c>.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddOperatorDataStore(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);
        services.AddHoneyDrunkDataEntityFramework<OperatorDbContext>(configureDbContext);
        services.RemoveAll<IOperatorStore>();
        services.TryAddSingleton<ApprovalDataService>();
        services.TryAddSingleton<PauseDataService>();
        services.TryAddSingleton<SpendDataService>();
        services.TryAddSingleton<PendingAuditDataService>();
        services.AddSingleton<IOperatorStore>(provider => new OperatorDataStore(
            provider.GetRequiredService<ApprovalDataService>(),
            provider.GetRequiredService<PauseDataService>(),
            provider.GetRequiredService<SpendDataService>(),
            provider.GetRequiredService<PendingAuditDataService>()));
        return services;
    }
}

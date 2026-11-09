using HoneyDrunk.Data.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>
/// Operator's tables in the <c>operator</c> schema. Times are stored as UTC ticks and money as whole micro-units, so
/// every comparison and sum runs in SQL on any provider, SQLite included.
/// </summary>
/// <param name="options">The context options.</param>
public sealed class OperatorDbContext(DbContextOptions<OperatorDbContext> options) : HoneyDrunkDbContext(options)
{
    /// <summary>Gets the approvals.</summary>
    public DbSet<ApprovalEntity> Approvals => this.Set<ApprovalEntity>();

    /// <summary>Gets the pauses.</summary>
    public DbSet<PauseEntity> Pauses => this.Set<PauseEntity>();

    /// <summary>Gets the spend per budget window.</summary>
    public DbSet<SpendEntity> Spend => this.Set<SpendEntity>();

    /// <summary>Gets the audit entries waiting for delivery.</summary>
    public DbSet<PendingAuditEntity> PendingAudits => this.Set<PendingAuditEntity>();

    /// <inheritdoc />
    protected override string? Schema => "operator";

    /// <inheritdoc />
    protected override void ApplyConfigurations(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new ApprovalEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PauseEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SpendEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PendingAuditEntityConfiguration());
    }
}

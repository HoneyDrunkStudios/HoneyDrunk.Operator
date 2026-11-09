using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoneyDrunk.Operator.Data;

/// <summary>Maps <see cref="PendingAuditEntity"/> to <c>operator.PendingAudits</c>.</summary>
internal sealed class PendingAuditEntityConfiguration : IEntityTypeConfiguration<PendingAuditEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PendingAuditEntity> builder)
    {
        builder.ToTable("PendingAudits");
        builder.HasKey(row => row.Sequence);
        builder.Property(row => row.Sequence).ValueGeneratedOnAdd();
        builder.HasIndex(row => row.Id).IsUnique();
        builder.HasIndex(row => new { row.DeliveredAtTicks, row.Sequence });
        builder.Property(row => row.EventName).HasMaxLength(200).IsRequired();
        builder.Property(row => row.Actor).HasMaxLength(200).IsRequired();
        builder.Property(row => row.Outcome).IsRequired();
        builder.Property(row => row.TargetType).HasMaxLength(100).IsRequired();
        builder.Property(row => row.TargetId).HasMaxLength(400).IsRequired();
        builder.Property(row => row.Reason).HasMaxLength(2000);
        builder.Property(row => row.CorrelationId).HasMaxLength(200);
        builder.Property(row => row.CreatedAtTicks).IsRequired();
    }
}

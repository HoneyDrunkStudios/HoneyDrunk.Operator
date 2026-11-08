using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoneyDrunk.Operator.Data;

/// <summary>Maps <see cref="ApprovalEntity"/> to <c>operator.Approvals</c>.</summary>
internal sealed class ApprovalEntityConfiguration : IEntityTypeConfiguration<ApprovalEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ApprovalEntity> builder)
    {
        builder.ToTable("Approvals");
        builder.HasKey(row => row.ApprovalId);
        builder.Property(row => row.ApprovalId).HasMaxLength(200).IsRequired();
        builder.Property(row => row.Subject).HasMaxLength(200).IsRequired();
        builder.Property(row => row.Action).HasMaxLength(200).IsRequired();
        builder.Property(row => row.ContextJson).IsRequired();
        builder.Property(row => row.RequestedScope).HasMaxLength(400).IsRequired();
        builder.Property(row => row.ExpiryTicks).IsRequired();
        builder.Property(row => row.RequesterCorrelationId).HasMaxLength(200).IsRequired();
        builder.Property(row => row.RequestedAtTicks).IsRequired();
        builder.Property(row => row.Outcome).IsRequired();
        builder.Property(row => row.ApproverIdentity).HasMaxLength(200).IsRequired();
        builder.Property(row => row.DecidedAtTicks).IsRequired();
        builder.Property(row => row.Reason).HasMaxLength(500);
        builder.HasIndex(row => new { row.Outcome, row.RequestedAtTicks });
    }
}

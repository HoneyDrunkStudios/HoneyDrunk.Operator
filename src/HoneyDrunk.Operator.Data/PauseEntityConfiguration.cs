using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoneyDrunk.Operator.Data;

/// <summary>Maps <see cref="PauseEntity"/> to <c>operator.Pauses</c>.</summary>
internal sealed class PauseEntityConfiguration : IEntityTypeConfiguration<PauseEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PauseEntity> builder)
    {
        builder.ToTable("Pauses");
        builder.HasKey(row => row.Name);
        builder.Property(row => row.Name).HasMaxLength(200).IsRequired();
        builder.Property(row => row.PausedBy).HasMaxLength(200).IsRequired();
        builder.Property(row => row.Reason).HasMaxLength(500).IsRequired();
        builder.Property(row => row.SinceTicks).IsRequired();
    }
}

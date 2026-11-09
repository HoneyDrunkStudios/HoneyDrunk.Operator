using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoneyDrunk.Operator.Data;

/// <summary>Maps <see cref="SpendEntity"/> to <c>operator.Spend</c>.</summary>
internal sealed class SpendEntityConfiguration : IEntityTypeConfiguration<SpendEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SpendEntity> builder)
    {
        builder.ToTable("Spend");
        builder.HasKey(row => new { row.Scope, row.Window });
        builder.Property(row => row.Scope).HasMaxLength(200).IsRequired();
        builder.Property(row => row.Window).HasMaxLength(50).IsRequired();
        builder.Property(row => row.AmountMicros).IsRequired();
    }
}

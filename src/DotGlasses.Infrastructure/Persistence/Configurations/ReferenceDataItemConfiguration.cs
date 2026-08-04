using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class ReferenceDataItemConfiguration : IEntityTypeConfiguration<ReferenceDataItem>
{
    public void Configure(EntityTypeBuilder<ReferenceDataItem> builder)
    {
        builder.ToTable("ReferenceDataItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.Category, x.Code }).IsUnique();
    }
}

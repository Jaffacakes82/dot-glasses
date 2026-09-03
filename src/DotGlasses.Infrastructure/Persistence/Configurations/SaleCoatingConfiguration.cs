using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class SaleCoatingConfiguration : IEntityTypeConfiguration<SaleCoating>
{
    public void Configure(EntityTypeBuilder<SaleCoating> builder)
    {
        builder.ToTable("SaleCoatings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.SaleId);
        builder.HasIndex(x => new { x.SaleId, x.CoatingRefId }).IsUnique();
    }
}

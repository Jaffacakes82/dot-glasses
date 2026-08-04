using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class LensOptionConfiguration : IEntityTypeConfiguration<LensOption>
{
    public void Configure(EntityTypeBuilder<LensOption> builder)
    {
        builder.ToTable("LensOptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SphericalPower).HasPrecision(4, 2);
        builder.Property(x => x.AddPower).HasPrecision(4, 2);
        builder.HasIndex(x => x.PresetCatalogueId);
        builder.HasIndex(x => x.CoatingId);
    }
}

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
        builder.Property(x => x.Label).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LensTypeOtherText).HasMaxLength(200);

        // The same precision as a record's per-eye lens power columns (SaleConfiguration and its
        // siblings), so a lens in a set and a lens on a record compare by value (ADR-0007).
        builder.Property(x => x.Sphere).HasPrecision(5, 2);
        builder.Property(x => x.Cylinder).HasPrecision(5, 2);
        builder.Property(x => x.Axis).HasPrecision(5, 2);
        builder.Property(x => x.Add).HasPrecision(5, 2);

        builder.HasIndex(x => x.PresetCatalogueId);
    }
}

public class LensOptionCoatingConfiguration : IEntityTypeConfiguration<LensOptionCoating>
{
    public void Configure(EntityTypeBuilder<LensOptionCoating> builder)
    {
        builder.ToTable("LensOptionCoatings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.LensOptionId, x.CoatingRefId }).IsUnique();

        // Cascade: a lens's coatings mean nothing without the lens, and removing a lens from its
        // set (a hard delete — nothing references a lens row by foreign key) takes them with it.
        builder.HasOne<LensOption>()
            .WithMany()
            .HasForeignKey(x => x.LensOptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LensOptionCoatingPairingConfiguration : IEntityTypeConfiguration<LensOptionCoatingPairing>
{
    public void Configure(EntityTypeBuilder<LensOptionCoatingPairing> builder)
    {
        builder.ToTable("LensOptionCoatingPairings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.LensOptionId, x.TriggerCoatingRefId, x.PairedCoatingRefId }).IsUnique();

        // Cascade, for the same reason as LensOptionCoatingConfiguration.
        builder.HasOne<LensOption>()
            .WithMany()
            .HasForeignKey(x => x.LensOptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

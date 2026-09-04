using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.OccupationOtherText).HasMaxLength(200);
        builder.Property(x => x.FrameColourOtherText).HasMaxLength(200);
        builder.Property(x => x.HardCaseOtherColourText).HasMaxLength(200);
        builder.Property(x => x.ReferralOtherText).HasMaxLength(200);
        builder.Property(x => x.ReferralLocationFreeText).HasMaxLength(500);
        builder.Property(x => x.LensTypeOtherText).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);

        foreach (var propertyName in new[]
                 {
                     nameof(Sale.CustomSphereLeft), nameof(Sale.CustomCylinderLeft), nameof(Sale.CustomAxisLeft), nameof(Sale.CustomAddPowerLeft),
                     nameof(Sale.CustomSphereRight), nameof(Sale.CustomCylinderRight), nameof(Sale.CustomAxisRight), nameof(Sale.CustomAddPowerRight),
                 })
        {
            builder.Property(propertyName).HasPrecision(5, 2);
        }

        builder.Property(x => x.PupilDistanceMm).HasPrecision(4, 1);

        builder.HasIndex(x => x.HierarchyPath);
        builder.HasIndex(x => x.TechnicianUserId);
        builder.HasIndex(x => x.CustomerId);
    }
}

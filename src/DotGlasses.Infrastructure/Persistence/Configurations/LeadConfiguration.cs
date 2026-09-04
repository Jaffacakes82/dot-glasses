using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.OccupationOtherText).HasMaxLength(200);
        builder.Property(x => x.ReasonNotPurchasedOtherText).HasMaxLength(200);
        builder.Property(x => x.ReferralOtherText).HasMaxLength(200);
        builder.Property(x => x.ReferralLocationFreeText).HasMaxLength(500);
        builder.Property(x => x.LensTypeOtherText).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);

        foreach (var propertyName in new[]
                 {
                     nameof(Lead.CustomSphereLeft), nameof(Lead.CustomCylinderLeft), nameof(Lead.CustomAxisLeft), nameof(Lead.CustomAddPowerLeft),
                     nameof(Lead.CustomSphereRight), nameof(Lead.CustomCylinderRight), nameof(Lead.CustomAxisRight), nameof(Lead.CustomAddPowerRight),
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

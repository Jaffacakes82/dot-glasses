using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class TestConfiguration : IEntityTypeConfiguration<Test>
{
    public void Configure(EntityTypeBuilder<Test> builder)
    {
        builder.ToTable("Tests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.OccupationOtherText).HasMaxLength(200);
        builder.Property(x => x.ReferralOtherText).HasMaxLength(200);
        builder.Property(x => x.ReferralLocationFreeText).HasMaxLength(500);
        builder.Property(x => x.LensTypeOtherText).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);

        foreach (var propertyName in new[]
                 {
                     nameof(Test.CustomSphereLeft), nameof(Test.CustomCylinderLeft), nameof(Test.CustomAxisLeft), nameof(Test.CustomAddPowerLeft),
                     nameof(Test.CustomSphereRight), nameof(Test.CustomCylinderRight), nameof(Test.CustomAxisRight), nameof(Test.CustomAddPowerRight),
                 })
        {
            builder.Property(propertyName).HasPrecision(5, 2);
        }

        builder.Property(x => x.PupilDistanceMm).HasPrecision(4, 1);

        builder.HasIndex(x => x.HierarchyPath);
        builder.HasIndex(x => x.TechnicianUserId);
    }
}

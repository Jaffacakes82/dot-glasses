using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class LensStrengthCoatingOptionConfiguration : IEntityTypeConfiguration<LensStrengthCoatingOption>
{
    public void Configure(EntityTypeBuilder<LensStrengthCoatingOption> builder)
    {
        builder.ToTable("LensStrengthCoatingOptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.LensStrengthRefId);
        builder.HasIndex(x => x.CoatingRefId);
        builder.HasIndex(x => new { x.LensStrengthRefId, x.CoatingRefId }).IsUnique();
    }
}

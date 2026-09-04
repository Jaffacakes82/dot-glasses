using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class CoatingExclusionConfiguration : IEntityTypeConfiguration<CoatingExclusion>
{
    public void Configure(EntityTypeBuilder<CoatingExclusion> builder)
    {
        builder.ToTable("CoatingExclusions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.CoatingRefIdA);
        builder.HasIndex(x => x.CoatingRefIdB);

        // CoatingRefIdA/B are canonicalized (lower Guid first) at write time — see CoatingExclusion's
        // doc comment — so this unique index is enough to prevent a pair being stored twice, without
        // needing a check-constraint to enforce the ordering itself.
        builder.HasIndex(x => new { x.CoatingRefIdA, x.CoatingRefIdB }).IsUnique();
    }
}

using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class PresetCatalogueAssignmentConfiguration : IEntityTypeConfiguration<PresetCatalogueAssignment>
{
    public void Configure(EntityTypeBuilder<PresetCatalogueAssignment> builder)
    {
        builder.ToTable("PresetCatalogueAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.PresetCatalogueId);
        builder.HasIndex(x => x.OrgNodeId);
        builder.HasIndex(x => new { x.PresetCatalogueId, x.OrgNodeId }).IsUnique();
    }
}

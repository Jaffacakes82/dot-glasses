using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

/// <summary>
/// Assigns both seeded catalogues to the seeded Kenya country node, cascading down to the seeded
/// retailer/retail point per PresetCatalogue's "assign to any sub-organisation" model — gives the
/// seeded org tree something real to demonstrate the assignment mechanism with.
/// </summary>
public class PresetCatalogueAssignmentSeedConfiguration : IEntityTypeConfiguration<PresetCatalogueAssignment>
{
    public void Configure(EntityTypeBuilder<PresetCatalogueAssignment> builder)
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            new PresetCatalogueAssignment
            {
                Id = new("e0000000-0000-0000-0000-000000000001"),
                PresetCatalogueId = PresetCatalogueSeedConfiguration.SixLensSetId,
                OrgNodeId = OrganisationSeedConfiguration.KenyaId,
                CreatedAtUtc = now,
            },
            new PresetCatalogueAssignment
            {
                Id = new("e0000000-0000-0000-0000-000000000002"),
                PresetCatalogueId = PresetCatalogueSeedConfiguration.NineLensSetId,
                OrgNodeId = OrganisationSeedConfiguration.KenyaId,
                CreatedAtUtc = now,
            });
    }
}

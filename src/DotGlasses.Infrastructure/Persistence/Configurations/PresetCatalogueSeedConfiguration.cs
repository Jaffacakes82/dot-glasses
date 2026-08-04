using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

/// <summary>
/// Seeds the two standard preset catalogues (HasData, fixed GUIDs) with the exact dioptre values
/// Bradley gave on the call (cross-checked against the Kobo dioptres_six/dioptres_nine/_add
/// lists) — owned by the seeded DGI root (OrganisationSeedConfiguration.DgiId). No "Classical"
/// catalogue is seeded (decision: dropped entirely, see ReferenceDataSeedConfiguration).
///
/// Every non-bifocal lens defaults to Clear (an assumption — the call only specified the forced
/// Photochromic coating for bifocals; DGI can change any lens's coating via the future admin UI).
/// Every bifocal is forced to Photochromic per the call ("the only bifocals we sell are
/// photochromic").
/// </summary>
public class PresetCatalogueSeedConfiguration : IEntityTypeConfiguration<PresetCatalogue>
{
    public static readonly Guid SixLensSetId = new("c0000000-0000-0000-0000-000000000001");
    public static readonly Guid NineLensSetId = new("c0000000-0000-0000-0000-000000000002");

    public void Configure(EntityTypeBuilder<PresetCatalogue> builder)
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            new PresetCatalogue { Id = SixLensSetId, Name = "6-Lens Set", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId, CreatedAtUtc = now },
            new PresetCatalogue { Id = NineLensSetId, Name = "9-Lens Set", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId, CreatedAtUtc = now });
    }
}

public class LensOptionSeedConfiguration : IEntityTypeConfiguration<LensOption>
{
    public void Configure(EntityTypeBuilder<LensOption> builder)
    {
        var options = new List<LensOption>();
        var sort = 0;

        void Standard(Guid id, Guid catalogueId, decimal sphere)
        {
            options.Add(new LensOption
            {
                Id = id,
                PresetCatalogueId = catalogueId,
                SphericalPower = sphere,
                IsBifocal = false,
                AddPower = null,
                CoatingId = ReferenceDataSeedConfiguration.CoatingClearId,
                SortOrder = sort++,
            });
        }

        void Bifocal(Guid id, Guid catalogueId, decimal addPower)
        {
            options.Add(new LensOption
            {
                Id = id,
                PresetCatalogueId = catalogueId,
                SphericalPower = 0.00m,
                IsBifocal = true,
                AddPower = addPower,
                CoatingId = ReferenceDataSeedConfiguration.CoatingPhotochromicId,
                SortOrder = sort++,
            });
        }

        // 6-Lens Set: +2.50, +1.25, +0.00, -1.50, -3.00, -4.50, plus bifocals 0.00/+2.50, 0.00/+1.25.
        var six = PresetCatalogueSeedConfiguration.SixLensSetId;
        sort = 0;
        Standard(new("d0000000-0000-0000-0000-000000000001"), six, 2.50m);
        Standard(new("d0000000-0000-0000-0000-000000000002"), six, 1.25m);
        Standard(new("d0000000-0000-0000-0000-000000000003"), six, 0.00m);
        Standard(new("d0000000-0000-0000-0000-000000000004"), six, -1.50m);
        Standard(new("d0000000-0000-0000-0000-000000000005"), six, -3.00m);
        Standard(new("d0000000-0000-0000-0000-000000000006"), six, -4.50m);
        Bifocal(new("d0000000-0000-0000-0000-000000000007"), six, 2.50m);
        Bifocal(new("d0000000-0000-0000-0000-000000000008"), six, 1.25m);

        // 9-Lens Set: +3.00, +2.00, +1.25, +0.00, -1.00, -1.50, -2.00, -2.50, -4.00,
        // plus bifocals 0.00/+3.00, 0.00/+2.00, 0.00/+1.25.
        var nine = PresetCatalogueSeedConfiguration.NineLensSetId;
        sort = 0;
        Standard(new("d0000000-0000-0000-0000-000000000009"), nine, 3.00m);
        Standard(new("d0000000-0000-0000-0000-000000000010"), nine, 2.00m);
        Standard(new("d0000000-0000-0000-0000-000000000011"), nine, 1.25m);
        Standard(new("d0000000-0000-0000-0000-000000000012"), nine, 0.00m);
        Standard(new("d0000000-0000-0000-0000-000000000013"), nine, -1.00m);
        Standard(new("d0000000-0000-0000-0000-000000000014"), nine, -1.50m);
        Standard(new("d0000000-0000-0000-0000-000000000015"), nine, -2.00m);
        Standard(new("d0000000-0000-0000-0000-000000000016"), nine, -2.50m);
        Standard(new("d0000000-0000-0000-0000-000000000017"), nine, -4.00m);
        Bifocal(new("d0000000-0000-0000-0000-000000000018"), nine, 3.00m);
        Bifocal(new("d0000000-0000-0000-0000-000000000019"), nine, 2.00m);
        Bifocal(new("d0000000-0000-0000-0000-000000000020"), nine, 1.25m);

        builder.HasData(options);
    }
}

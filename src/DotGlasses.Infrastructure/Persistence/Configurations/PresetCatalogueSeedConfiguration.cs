using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

/// <summary>
/// Seeds the two standard preset catalogues (HasData, fixed GUIDs) with the exact dioptre values
/// Bradley gave on the call (cross-checked against the Kobo dioptres_six/dioptres_nine/_add
/// lists) — owned by the seeded DGI root (OrganisationSeedConfiguration.DgiId). No "Classical"
/// catalogue is seeded (decision: dropped entirely, see ReferenceDataSeedConfiguration).
/// </summary>
public class PresetCatalogueSeedConfiguration : IEntityTypeConfiguration<PresetCatalogue>
{
    public static readonly Guid SixLensSetId = new("c0000000-0000-0000-0000-000000000001");
    public static readonly Guid NineLensSetId = new("c0000000-0000-0000-0000-000000000002");

    public void Configure(EntityTypeBuilder<PresetCatalogue> builder)
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            new PresetCatalogue
            {
                Id = SixLensSetId,
                Name = "6-Lens Set",
                Description = "Standard six-option lens range for outlets with local stock.",
                RangeDescription = "+2.50 to -4.50",
                OwningOrgNodeId = OrganisationSeedConfiguration.DgiId,
                CreatedAtUtc = now,
            },
            new PresetCatalogue
            {
                Id = NineLensSetId,
                Name = "9-Lens Set",
                Description = "Extended nine-option lens range for outlets with wider stock.",
                RangeDescription = "+3.00 to -4.00",
                OwningOrgNodeId = OrganisationSeedConfiguration.DgiId,
                CreatedAtUtc = now,
            });
    }
}

/// <summary>
/// Each catalogue's LensOption roster now just picks LensStrength reference items (2026-08-05
/// rework) — no typed power/bifocal/coating fields here anymore, see LensOption's own doc
/// comment. Roster membership (which strengths belong to 6-Lens vs 9-Lens) is unchanged from
/// before the rework.
/// </summary>
public class LensOptionSeedConfiguration : IEntityTypeConfiguration<LensOption>
{
    public void Configure(EntityTypeBuilder<LensOption> builder)
    {
        var options = new List<LensOption>();
        var sort = 0;

        void Add(Guid id, Guid catalogueId, Guid lensStrengthRefId)
        {
            options.Add(new LensOption
            {
                Id = id,
                PresetCatalogueId = catalogueId,
                LensStrengthRefId = lensStrengthRefId,
                SortOrder = sort++,
            });
        }

        // 6-Lens Set: +2.50, +1.25, +0.00, -1.50, -3.00, -4.50, plus bifocals 0.00/+2.50, 0.00/+1.25.
        var six = PresetCatalogueSeedConfiguration.SixLensSetId;
        sort = 0;
        Add(new("d0000000-0000-0000-0000-000000000001"), six, ReferenceDataSeedConfiguration.LensStrength250Id);
        Add(new("d0000000-0000-0000-0000-000000000002"), six, ReferenceDataSeedConfiguration.LensStrength125Id);
        Add(new("d0000000-0000-0000-0000-000000000003"), six, ReferenceDataSeedConfiguration.LensStrength000Id);
        Add(new("d0000000-0000-0000-0000-000000000004"), six, ReferenceDataSeedConfiguration.LensStrengthMinus150Id);
        Add(new("d0000000-0000-0000-0000-000000000005"), six, ReferenceDataSeedConfiguration.LensStrengthMinus300Id);
        Add(new("d0000000-0000-0000-0000-000000000006"), six, ReferenceDataSeedConfiguration.LensStrengthMinus450Id);
        Add(new("d0000000-0000-0000-0000-000000000007"), six, ReferenceDataSeedConfiguration.LensStrengthBifocal250Id);
        Add(new("d0000000-0000-0000-0000-000000000008"), six, ReferenceDataSeedConfiguration.LensStrengthBifocal125Id);

        // 9-Lens Set: +3.00, +2.00, +1.25, +0.00, -1.00, -1.50, -2.00, -2.50, -4.00,
        // plus bifocals 0.00/+3.00, 0.00/+2.00, 0.00/+1.25.
        var nine = PresetCatalogueSeedConfiguration.NineLensSetId;
        sort = 0;
        Add(new("d0000000-0000-0000-0000-000000000009"), nine, ReferenceDataSeedConfiguration.LensStrength300Id);
        Add(new("d0000000-0000-0000-0000-000000000010"), nine, ReferenceDataSeedConfiguration.LensStrength200Id);
        Add(new("d0000000-0000-0000-0000-000000000011"), nine, ReferenceDataSeedConfiguration.LensStrength125Id);
        Add(new("d0000000-0000-0000-0000-000000000012"), nine, ReferenceDataSeedConfiguration.LensStrength000Id);
        Add(new("d0000000-0000-0000-0000-000000000013"), nine, ReferenceDataSeedConfiguration.LensStrengthMinus100Id);
        Add(new("d0000000-0000-0000-0000-000000000014"), nine, ReferenceDataSeedConfiguration.LensStrengthMinus150Id);
        Add(new("d0000000-0000-0000-0000-000000000015"), nine, ReferenceDataSeedConfiguration.LensStrengthMinus200Id);
        Add(new("d0000000-0000-0000-0000-000000000016"), nine, ReferenceDataSeedConfiguration.LensStrengthMinus250Id);
        Add(new("d0000000-0000-0000-0000-000000000017"), nine, ReferenceDataSeedConfiguration.LensStrengthMinus400Id);
        Add(new("d0000000-0000-0000-0000-000000000018"), nine, ReferenceDataSeedConfiguration.LensStrengthBifocal300Id);
        Add(new("d0000000-0000-0000-0000-000000000019"), nine, ReferenceDataSeedConfiguration.LensStrengthBifocal200Id);
        Add(new("d0000000-0000-0000-0000-000000000020"), nine, ReferenceDataSeedConfiguration.LensStrengthBifocal125Id);

        builder.HasData(options);
    }
}

/// <summary>
/// Which coatings each LensStrength is available in (2026-08-05, new — see LensOption's doc
/// comment for why this replaced a single forced CoatingId). Only the one fact actually known
/// from the CEO call is seeded: every bifocal is Photochromic-only. The ~12 non-bifocal strengths
/// ship with zero configured coatings — a real, visible interim gap; DGI configures the real
/// matrix via the Preset Catalogues admin screen. See CLAUDE.md's [OPEN] items.
/// </summary>
public class LensStrengthCoatingOptionSeedConfiguration : IEntityTypeConfiguration<LensStrengthCoatingOption>
{
    public void Configure(EntityTypeBuilder<LensStrengthCoatingOption> builder)
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

        LensStrengthCoatingOption Photochromic(Guid id, Guid lensStrengthRefId) => new()
        {
            Id = id,
            LensStrengthRefId = lensStrengthRefId,
            CoatingRefId = ReferenceDataSeedConfiguration.CoatingPhotochromicId,
            CreatedAtUtc = now,
        };

        builder.HasData(
            Photochromic(new("e1000000-0000-0000-0000-000000000001"), ReferenceDataSeedConfiguration.LensStrengthBifocal300Id),
            Photochromic(new("e1000000-0000-0000-0000-000000000002"), ReferenceDataSeedConfiguration.LensStrengthBifocal250Id),
            Photochromic(new("e1000000-0000-0000-0000-000000000003"), ReferenceDataSeedConfiguration.LensStrengthBifocal200Id),
            Photochromic(new("e1000000-0000-0000-0000-000000000004"), ReferenceDataSeedConfiguration.LensStrengthBifocal125Id));
    }
}

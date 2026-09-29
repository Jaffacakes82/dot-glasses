using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Rules.LensPowers;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>
/// Example 6-Lens and 9-Lens sets in the lens-power shape (ADR-0007), so local dev and the test
/// suites have something to sell from without an admin building sets by hand. The powers are the
/// rosters the two original seeded sets carried; each lens
/// has a typed label, real coatings (every bifocal Photochromic-only, the one coating fact known
/// for them), and the 6-Lens +2.50 carries the one example pairing, Blue block →
/// Photochromic. Both are owned by DGI and assigned to Kenya, as the original seeded sets were.
///
/// <b>Never part of a migration, and never run against staging or production</b>: those
/// environments get no active lens sets until DGI builds the real ones (the lens-power spec's
/// handover note). Only two callers exist — Program.cs's development-only startup block and the
/// test fixtures — and that is deliberate. This is ordinary data written through the model, not
/// <c>HasData</c>, precisely so that no migration ever carries it.
///
/// Idempotent per set: a set that already exists (retired included) is left exactly as it is, so
/// a developer's edits to it survive a restart.
/// </summary>
public static class ExampleLensSets
{
    public static readonly Guid SixLensSetId = new("c1000000-0000-0000-0000-000000000001");
    public static readonly Guid NineLensSetId = new("c1000000-0000-0000-0000-000000000002");

    /// <summary>The 6-Lens +2.50 — the lens carrying the example pairing.</summary>
    public static readonly Guid SixLensPlus250Id = LensId(1);

    public static async Task EnsureSeededAsync(DotGlassesDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.PresetCatalogues.IgnoreQueryFilters()
            .Where(c => c.Id == SixLensSetId || c.Id == NineLensSetId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (!existing.Contains(SixLensSetId))
        {
            AddSet(dbContext, SixLensSetId, "6-Lens Set", "Example six-power lens set for outlets with local stock.",
                singleVision: [2.50m, 1.25m, 0.00m, -1.50m, -3.00m, -4.50m],
                bifocalAdds: [2.50m, 1.25m],
                firstLensNumber: 1);
        }

        if (!existing.Contains(NineLensSetId))
        {
            AddSet(dbContext, NineLensSetId, "9-Lens Set", "Example nine-power lens set for outlets with wider stock.",
                singleVision: [3.00m, 2.00m, 1.25m, 0.00m, -1.00m, -1.50m, -2.00m, -2.50m, -4.00m],
                bifocalAdds: [3.00m, 2.00m, 1.25m],
                firstLensNumber: 101);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void AddSet(
        DotGlassesDbContext dbContext, Guid setId, string name, string description,
        IReadOnlyList<decimal> singleVision, IReadOnlyList<decimal> bifocalAdds, int firstLensNumber)
    {
        var now = DateTimeOffset.UtcNow;
        dbContext.PresetCatalogues.Add(new PresetCatalogue
        {
            Id = setId,
            Name = name,
            Description = description,
            OwningOrgNodeId = OrganisationSeedConfiguration.DgiId,
            CreatedAtUtc = now,
        });
        dbContext.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment
        {
            Id = Guid.NewGuid(),
            PresetCatalogueId = setId,
            OrgNodeId = OrganisationSeedConfiguration.KenyaId,
            CreatedAtUtc = now,
        });

        var number = firstLensNumber;
        foreach (var sphere in singleVision)
        {
            var lens = AddLens(dbContext, setId, LensId(number++), LensPowerValues.FormatPower(sphere), sphere, add: null,
                [ReferenceDataSeedConfiguration.CoatingClearId, ReferenceDataSeedConfiguration.CoatingBlueBlockId, ReferenceDataSeedConfiguration.CoatingPhotochromicId]);

            if (lens.Id == SixLensPlus250Id)
            {
                dbContext.LensOptionCoatingPairings.Add(new LensOptionCoatingPairing
                {
                    Id = Guid.NewGuid(),
                    LensOptionId = lens.Id,
                    TriggerCoatingRefId = ReferenceDataSeedConfiguration.CoatingBlueBlockId,
                    PairedCoatingRefId = ReferenceDataSeedConfiguration.CoatingPhotochromicId,
                });
            }
        }

        foreach (var add in bifocalAdds)
        {
            AddLens(dbContext, setId, LensId(number++), $"Bifocal {LensPowerValues.FormatPower(add)}", sphere: 0.00m, add,
                [ReferenceDataSeedConfiguration.CoatingPhotochromicId]);
        }
    }

    private static LensOption AddLens(
        DotGlassesDbContext dbContext, Guid setId, Guid lensId, string label, decimal sphere, decimal? add, IReadOnlyList<Guid> coatingIds)
    {
        var lens = new LensOption
        {
            Id = lensId,
            PresetCatalogueId = setId,
            Label = label,
            Sphere = sphere,
            Add = add,
            LensTypeRefId = add is null ? null : ReferenceDataSeedConfiguration.LensTypeBifocalId,
        };
        dbContext.LensOptions.Add(lens);

        foreach (var coatingId in coatingIds)
        {
            dbContext.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = lensId, CoatingRefId = coatingId });
        }

        return lens;
    }

    private static Guid LensId(int number) => new($"d1000000-0000-0000-0000-{number:D12}");
}

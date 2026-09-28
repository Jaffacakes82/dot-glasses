using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;

namespace DotGlasses.Web.Tests;

/// <summary>A lens set lens in the lens-power shape (ADR-0007), for a test that builds its own lens
/// set: a single-vision power, its typed label, and one coating it comes in — the least a lens-set
/// Sale needs to pass the rules.</summary>
public static class LensSetTestData
{
    public static (Guid LensId, Guid CoatingId) AddSellableLens(DotGlassesDbContext db, Guid lensSetId, string label = "+2.50", decimal sphere = 2.50m)
    {
        var lensId = Guid.NewGuid();
        var coatingId = ReferenceDataSeedConfiguration.CoatingClearId;

        db.LensOptions.Add(new LensOption { Id = lensId, PresetCatalogueId = lensSetId, Label = label, Sphere = sphere });
        db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = lensId, CoatingRefId = coatingId });

        return (lensId, coatingId);
    }
}

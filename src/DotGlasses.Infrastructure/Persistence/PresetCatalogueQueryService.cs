using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.PresetCatalogues;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Answered entirely from IReferenceDataSnapshotProvider — the same snapshot the shared
/// rules check against — so the lens sets the Field App is offered and the lens sets the server
/// accepts are one definition, not two that could drift: "reaches this location" is
/// <see cref="Rules.ReferenceData.ReferenceDataSnapshot.ReachesLocation"/> in both.</summary>
public class PresetCatalogueQueryService(IReferenceDataSnapshotProvider referenceDataSnapshotProvider) : IPresetCatalogueQueryService
{
    public async Task<IReadOnlyList<PresetCatalogueDto>> ListAvailableForCallerAsync(string callerHierarchyPath, CancellationToken cancellationToken = default)
    {
        var here = (await referenceDataSnapshotProvider.GetAsync(cancellationToken)).AtLocation(callerHierarchyPath);

        // The Field App renders this list as it arrives (ADR-0005): every lens set assigned at or
        // above the caller, alphabetically; never a retired one (the snapshot keeps those for
        // historical labels), and never one with no lens powers — nothing on it could be sold.
        // Ordered here rather than by the provider's database ORDER BY, whose result depends on
        // the server's collation.
        return here.PresetCatalogues
            .Where(c => c.IsActive && here.ReachesLocation(c) && c.LensOptions.Count > 0)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new PresetCatalogueDto
            {
                Id = c.Id,
                Name = c.Name,
                LensOptions = c.LensOptions.Select(l => new LensOptionDto
                {
                    Id = l.Id,
                    Label = l.Label,
                    Sphere = l.Sphere,
                    Cylinder = l.Cylinder,
                    Axis = l.Axis,
                    Add = l.Add,
                    LensTypeRefId = l.LensTypeRefId,
                    LensTypeOtherText = l.LensTypeOtherText,
                    CoatingIds = l.CoatingIds,
                    Pairings = l.Pairings
                        .Select(p => new LensCoatingPairingDto { TriggerCoatingRefId = p.TriggerCoatingRefId, PairedCoatingRefId = p.PairedCoatingRefId })
                        .ToList(),
                }).ToList(),
            })
            .ToList();
    }
}

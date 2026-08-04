using DotGlasses.Contracts.PresetCatalogues;

namespace DotGlasses.Application.PresetCatalogues;

/// <summary>Read-only — backs the Field App's lens-range picker.</summary>
public interface IPresetCatalogueQueryService
{
    /// <summary>Catalogues assigned to callerHierarchyPath or any ancestor of it (see
    /// PresetCatalogueAssignment's doc comment — this is a deliberate deviation from the
    /// standard IHierarchyScoped filter, since a catalogue is created above the caller and must
    /// be visible below, the opposite direction).</summary>
    Task<IReadOnlyList<PresetCatalogueDto>> ListAvailableForCallerAsync(string callerHierarchyPath, CancellationToken cancellationToken = default);
}

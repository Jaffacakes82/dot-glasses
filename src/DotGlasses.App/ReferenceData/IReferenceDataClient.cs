using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// Fetches reference data + preset catalogues once per app session and caches in memory —
/// deliberately not IndexedDB-cached for offline use in this pass (needs connectivity to load;
/// degrades with a message if it can't reach the server, same pattern WidgetExamples.razor uses
/// for its own round trip). Full offline caching is a flagged follow-up, see CLAUDE.md.
/// </summary>
public interface IReferenceDataClient
{
    bool IsLoaded { get; }
    string? LoadError { get; }

    Task EnsureLoadedAsync();

    IReadOnlyList<ReferenceDataItemDto> GetByCategory(ReferenceDataCategory category);

    IReadOnlyList<PresetCatalogueDto> Catalogues { get; }
}

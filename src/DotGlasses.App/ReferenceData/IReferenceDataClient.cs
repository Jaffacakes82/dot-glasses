using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// Supplies the reference data + preset catalogues every consultation form's dropdowns are built
/// from. Fetched from the API when reachable and written through to IndexedDB, so a technician
/// with no connection this session still gets a working form from the last cached copy rather
/// than a dead end. Only a device that has genuinely never loaded them while online has nothing
/// to fall back on.
/// </summary>
public interface IReferenceDataClient
{
    bool IsLoaded { get; }

    string? LoadError { get; }

    /// <summary>True when the current data came from the IndexedDB cache rather than a live
    /// fetch — the forms surface this so a technician knows options may be out of date.</summary>
    bool IsFromCache { get; }

    /// <summary>When the cached copy was last refreshed from the server, if it came from cache.</summary>
    DateTimeOffset? CachedAtUtc { get; }

    Task EnsureLoadedAsync();

    /// <summary>Forces a re-fetch even when data is already loaded — the Retry action.</summary>
    Task RefreshAsync();

    IReadOnlyList<ReferenceDataItemDto> GetByCategory(ReferenceDataCategory category);

    IReadOnlyList<PresetCatalogueDto> Catalogues { get; }
}

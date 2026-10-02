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

    /// <summary>
    /// Loads for the signed-in technician's current location, fetching fresh whenever the server
    /// answers. Call it when a form opens (and from Retry) — not while one is being filled in, so
    /// its options don't change under the technician. A failed fetch keeps the copy this session
    /// already holds, else uses the cached one. Sign-in, sign-out and a location switch discard
    /// the held copy, so there is no "already loaded" shortcut to go stale.
    /// </summary>
    Task RefreshAsync();

    IReadOnlyList<ReferenceDataItemDto> GetByCategory(ReferenceDataCategory category);

    /// <summary>Every cached item across every category — what ReferenceDataSnapshotAdapter fills
    /// the shared rules' snapshot from. Active items only, because that is all the API returns.</summary>
    IReadOnlyList<ReferenceDataItemDto> AllItems { get; }

    /// <summary>What an image element should load for an item's picture: the copy kept on this
    /// device when there is one (so it shows offline), otherwise its address, or null when the
    /// item has no picture. See <see cref="FramePictureCache"/>.</summary>
    string? PictureSource(ReferenceDataItemDto item);

    IReadOnlyList<PresetCatalogueDto> Catalogues { get; }

    /// <summary>Coating exclusions (see ADR-0001) — cached the same way as the rest of reference
    /// data, for the same offline reasons. Pairings are per lens set lens now (ADR-0007) and
    /// arrive on each lens in <see cref="Catalogues"/>.</summary>
    IReadOnlyList<CoatingExclusionDto> CoatingExclusions { get; }
}

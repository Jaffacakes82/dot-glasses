using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.ReferenceData;

/// <summary>
/// Backs FluentValidation's reference-data checks (category correctness, "Other" free-text
/// requirement, LensOption/PresetCatalogue consistency) without validators needing
/// DotGlassesDbContext directly — Web must never reference Infrastructure except in Program.cs
/// (see CLAUDE.md's Architecture rules).
/// </summary>
public interface IReferenceDataLookupService
{
    /// <summary>Null if no ReferenceDataItem with this Id exists in this Category.</summary>
    Task<ReferenceDataLookupResult?> LookupAsync(Guid id, ReferenceDataCategory category, CancellationToken cancellationToken = default);

    Task<bool> LensOptionBelongsToCatalogueAsync(Guid lensOptionId, Guid presetCatalogueId, CancellationToken cancellationToken = default);

    /// <summary>The forced default coating for a preset LensOption (e.g. every bifocal is
    /// Photochromic) — SaleService derives Sale.CoatingRefId from this for preset ranges, never
    /// trusting a client-submitted coating for a lens the admin already pinned one to. Null if
    /// no LensOption with this Id exists.</summary>
    Task<Guid?> GetLensOptionCoatingIdAsync(Guid lensOptionId, CancellationToken cancellationToken = default);
}

public record ReferenceDataLookupResult(bool IsActive, bool IsOtherOption);

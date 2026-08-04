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
}

public record ReferenceDataLookupResult(bool IsActive, bool IsOtherOption);

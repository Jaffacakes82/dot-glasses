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

    /// <summary>True if coatingRefId is configured as available for the LensStrength the given
    /// LensOption references (LensStrengthCoatingOption) — replaces the old single forced
    /// CoatingId (2026-08-05 rework, see LensOption's own doc comment). False (not an exception)
    /// if the LensOption doesn't exist, or the strength has no coatings configured yet.</summary>
    Task<bool> IsCoatingAvailableForLensOptionAsync(Guid lensOptionId, Guid coatingRefId, CancellationToken cancellationToken = default);

    /// <summary>True if coatingRefIdA/coatingRefIdB can never both be present in the same
    /// Coating set (symmetric — checks both orderings) — see ADR-0001.</summary>
    Task<bool> AreCoatingsExcludedAsync(Guid coatingRefIdA, Guid coatingRefIdB, CancellationToken cancellationToken = default);
}

public record ReferenceDataLookupResult(bool IsActive, bool IsOtherOption);

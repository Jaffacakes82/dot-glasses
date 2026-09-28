using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.ReferenceData;

/// <summary>
/// A single reference-data row, looked up without the caller needing DotGlassesDbContext directly
/// — Web must never reference Infrastructure except in Program.cs (see CLAUDE.md's Architecture
/// rules).
///
/// Shrinking, not growing: ReferenceDataSnapshot answers all of this from a single read, and each
/// migration batch moved another question onto it. LensOptionBelongsToCatalogueAsync left with
/// ticket 10; IsCoatingAvailableForLensOptionAsync and AreCoatingsExcludedAsync left with
/// ticket 11, whose Coating rules were their only callers.
///
/// <b>The interface itself was expected to go with them and did not.</b> It serves Admin Portal
/// validators that run inside a <em>write</em> to the reference-data library, where the per-request
/// memoized snapshot is the one thing that must not be consulted (it may predate the write; see
/// ADR-0002 and CLAUDE.md). A direct row read is correct there, so this survives as an
/// Admin-Portal-write concern rather than a validation one. Its two callers
/// (AddLensOptionRequestValidator and SetCoatingAvailabilityBatchRequestValidator) went with the
/// Lens strength flow and the global coating grid (ADR-0007); it has none until the Add lens
/// dialog's validator (lens-power ticket 07), which the spec says must use it. Don't add a method
/// here for a consultation rule — add it to the snapshot.
/// </summary>
public interface IReferenceDataLookupService
{
    /// <summary>Null if no ReferenceDataItem with this Id exists in this Category.</summary>
    Task<ReferenceDataLookupResult?> LookupAsync(Guid id, ReferenceDataCategory category, CancellationToken cancellationToken = default);
}

public record ReferenceDataLookupResult(bool IsActive, bool IsOtherOption);

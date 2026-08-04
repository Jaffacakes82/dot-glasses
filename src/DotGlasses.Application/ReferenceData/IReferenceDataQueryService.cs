using DotGlasses.Contracts.ReferenceData;

namespace DotGlasses.Application.ReferenceData;

/// <summary>Read-only — backs the Field App's dropdown data. Separate from
/// IReferenceDataLookupService, which backs server-side validation and isn't DTO-shaped.</summary>
public interface IReferenceDataQueryService
{
    /// <summary>All active items across every category in one call — simpler client-side
    /// caching than a round trip per category.</summary>
    Task<IReadOnlyList<ReferenceDataItemDto>> ListActiveAsync(CancellationToken cancellationToken = default);
}

using DotGlasses.Contracts.ReferenceData;

namespace DotGlasses.Application.ReferenceData;

/// <summary>Read-only — backs the Field App's dropdown data. Separate from
/// IReferenceDataLookupService, which backs server-side validation and isn't DTO-shaped.</summary>
public interface IReferenceDataQueryService
{
    /// <summary>All active items across every category in one call — simpler client-side
    /// caching than a round trip per category.</summary>
    Task<IReadOnlyList<ReferenceDataItemDto>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Every Coating pairing/exclusion rule (see ADR-0001) — fetched/cached by the Field
    /// App alongside reference data so live pairing/exclusion enforcement works offline. No
    /// active-item filtering here: a rule referencing a since-retired Coating is harmless (that
    /// Coating can no longer be selected in the first place) and simpler to just return as-is.</summary>
    Task<CoatingRulesDto> GetCoatingRulesAsync(CancellationToken cancellationToken = default);
}

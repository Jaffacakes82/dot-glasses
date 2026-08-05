using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.ReferenceData;

/// <summary>Admin-only reference-data management — backs the Admin Portal's Reference Data
/// screen. Deliberately separate from IReferenceDataQueryService (which the Field App also
/// depends on, active items only): this returns every item including retired ones, and can
/// mutate. Uses Domain.Enums.ReferenceDataCategory directly rather than a Contracts-mirrored
/// enum, since DotGlasses.Web (the only consumer) has no restriction on referencing Domain — that
/// restriction exists specifically for DotGlasses.App, which transitively depends on
/// Contracts.</summary>
public interface IReferenceDataAdminService
{
    /// <summary>Every item, active and retired, ordered by Category then SortOrder.</summary>
    Task<IReadOnlyList<ReferenceDataAdminItem>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>True if the category already has an active item with IsOtherOption set —
    /// used to block creating a second one (consuming dropdowns key off this flag to reveal a
    /// free-text field, so two would be ambiguous).</summary>
    Task<bool> HasActiveOtherOptionAsync(ReferenceDataCategory category, CancellationToken cancellationToken = default);

    /// <summary>Code is derived from label (slugified), SortOrder is max+1 within the category.</summary>
    Task<ReferenceDataAdminItem> CreateAsync(ReferenceDataCategory category, string label, string? imageUrl, bool isOtherOption, CancellationToken cancellationToken = default);

    /// <summary>Soft-retire — IsActive = false. Never a hard delete: historical Test/Lead/Sale
    /// rows may still reference this item by Id.</summary>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);
}

public record ReferenceDataAdminItem(Guid Id, ReferenceDataCategory Category, string Code, string Label, int SortOrder, bool IsActive, bool IsOtherOption, string? ImageUrl);

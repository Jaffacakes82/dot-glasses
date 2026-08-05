using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// Generic admin-managed dropdown list entry. One table backs every reference-data category
/// (Occupation, ReasonNotPurchased, ReferralReason, Coating, FrameColour, HardCaseColour) rather
/// than six near-identical entities — matches both the Kobo choices-sheet shape it seeds from
/// (list_name/name/label) and the existing ReferenceDataController placeholder's "named lists of
/// items" shape.
/// </summary>
public class ReferenceDataItem : IAuditable, ISoftDeletable
{
    public Guid Id { get; set; }

    public ReferenceDataCategory Category { get; set; }

    /// <summary>Stable machine key, e.g. "farmer" — from the Kobo choices sheet's "name" column.</summary>
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>DGI can retire an option without deleting it, preserving historical records that
    /// reference it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Marks the "Other" row within a category so forms know to reveal a free-text field
    /// alongside the selection.</summary>
    public bool IsOtherOption { get; set; }

    /// <summary>Optional image shown alongside the option (e.g. a frame-colour swatch photo,
    /// matching the e-commerce site) — a plain admin-pasted URL for now, no upload/blob storage
    /// wired up yet. Generic on the entity but only meaningfully populated for FrameColour today.</summary>
    public string? ImageUrl { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

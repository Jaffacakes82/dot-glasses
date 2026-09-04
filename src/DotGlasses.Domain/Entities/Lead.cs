using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// Any record with contact details — even with no other information captured. Id is
/// client-generated (offline-sync outbox idempotency key).
/// </summary>
public class Lead : IAuditable, ISoftDeletable, IHierarchyScoped
{
    public Guid Id { get; set; }

    public string HierarchyPath { get; set; } = string.Empty;

    public Guid TechnicianUserId { get; set; }

    /// <summary>Required — a Lead always has contact details.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Set if this Lead was flipped from a Test (age/gender pre-populate from it).</summary>
    public Guid? SourceTestId { get; set; }

    public int? AgeYears { get; set; }

    public Gender Gender { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = Occupation).</summary>
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }

    public bool ConsentGiven { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = ReasonNotPurchased).</summary>
    public Guid ReasonNotPurchasedRefId { get; set; }
    public string? ReasonNotPurchasedOtherText { get; set; }

    /// <summary>Nullable — a Lead can carry no product preference at all (test results only).</summary>
    public LensRangeType? LensRangeType { get; set; }

    public Guid? PresetCatalogueId { get; set; }
    public Guid? LensOptionLeftId { get; set; }
    public Guid? LensOptionRightId { get; set; }

    // Custom-range prescription, set only when LensRangeType == Custom.
    public decimal? CustomSphereLeft { get; set; }
    public decimal? CustomCylinderLeft { get; set; }
    public decimal? CustomAxisLeft { get; set; }
    public decimal? CustomAddPowerLeft { get; set; }
    public decimal? CustomSphereRight { get; set; }
    public decimal? CustomCylinderRight { get; set; }
    public decimal? CustomAxisRight { get; set; }
    public decimal? CustomAddPowerRight { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = LensType) — asked when a Custom lens carries
    /// two distinct powers (an add power alongside its base sphere) on either eye.</summary>
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }

    /// <summary>The real inter-pupillary distance in mm — meaningful only for Custom range (see
    /// PresetPupilDistanceBucket for the preset-range equivalent).</summary>
    public decimal? PupilDistanceMm { get; set; }

    /// <summary>Meaningful only for a preset range (SixLensSet/NineLensSet) — a coarse 0-4 PD
    /// shorthand/frame-fit bucket (0-2 when ChildrensFrame), not a millimetre value, per the CEO
    /// call (2026-08-05 decision — kept as its own field rather than overloading
    /// PupilDistanceMm, which stays the real 54-74mm Custom-range value it always was).</summary>
    public int? PresetPupilDistanceBucket { get; set; }

    public bool ChildrensFrame { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = Coating). Optional per the CEO call — some
    /// leads only carry test results with no known product preference.</summary>
    public Guid? CoatingPreferenceRefId { get; set; }

    public bool ConvertedFlag { get; set; }
    public Guid? SaleId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

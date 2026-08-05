using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// Full transaction — a completed sale, whether fulfilled from local stock (preset range) or
/// routed to fulfilment (Custom + OrderFromDotGlasses). A custom order counts as a completed Sale
/// immediately — FulfilmentStatus tracks it through the lab/pickup workflow on this same row
/// (2026-08-05 decision) rather than a separate entity, matching the flat single-status queue the
/// Custom Orders admin screen shows; Id is client-generated (offline-sync outbox idempotency key).
/// </summary>
public class Sale : IAuditable, ISoftDeletable, IHierarchyScoped
{
    public Guid Id { get; set; }

    public string HierarchyPath { get; set; } = string.Empty;

    public Guid TechnicianUserId { get; set; }

    /// <summary>Required — full name is mandatory for a Sale.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Set if this Sale converts a Lead (also sets Lead.ConvertedFlag/SaleId).</summary>
    public Guid? SourceLeadId { get; set; }

    public int? AgeYears { get; set; }

    public Gender Gender { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = Occupation).</summary>
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }

    public bool ConsentGiven { get; set; }

    public LensRangeType LensRangeType { get; set; }

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

    /// <summary>Only meaningful when LensRangeType == Custom — routes the record to fulfilment
    /// (needs manufacturing + delivery) rather than logging it as stock already on hand.</summary>
    public bool OrderFromDotGlasses { get; set; }

    /// <summary>Null unless OrderFromDotGlasses is true — set to Submitted at creation, then
    /// advanced forward-only by the Custom Orders admin screen. See FulfilmentStatus.</summary>
    public FulfilmentStatus? FulfilmentStatus { get; set; }

    public decimal? PupilDistanceMm { get; set; }
    public bool ChildrensFrame { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = FrameColour).</summary>
    public Guid FrameColourRefId { get; set; }
    public string? FrameColourOtherText { get; set; }
    public FrameCoverage FrameCoverage { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = Coating) — client-submitted, validated in the
    /// Application layer against the legal set for the chosen lens (any active Coating for
    /// Custom; the configured LensStrengthCoatingOption set for a preset range).</summary>
    public Guid CoatingRefId { get; set; }

    public bool HardCaseSold { get; set; }
    /// <summary>FK to ReferenceDataItem (Category = HardCaseColour), set only when HardCaseSold.</summary>
    public Guid? HardCaseColourRefId { get; set; }
    public string? HardCaseOtherColourText { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

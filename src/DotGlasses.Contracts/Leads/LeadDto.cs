using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.Leads;

public class LeadDto
{
    public Guid Id { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
    public Guid TechnicianUserId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Read from the linked Customer — not stored on Lead itself, but included here so a
    /// Sale-conversion flow (Field App or Admin Portal) can prefill name/phone without a second
    /// round trip.</summary>
    public string CustomerFullName { get; set; } = string.Empty;
    public string? CustomerPhoneNumber { get; set; }
    public Guid? SourceTestId { get; set; }
    public int? AgeYears { get; set; }
    public Gender Gender { get; set; }
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }
    public bool ConsentGiven { get; set; }
    public bool ReferredOrTreated { get; set; }
    public Guid? ReferralReasonRefId { get; set; }
    public string? ReferralOtherText { get; set; }
    public string? ReferralLocationFreeText { get; set; }
    public bool TreatedInFacility { get; set; }
    public Guid ReasonNotPurchasedRefId { get; set; }
    public string? ReasonNotPurchasedOtherText { get; set; }

    /// <summary>Null on a Lead recorded before the question was asked.</summary>
    public bool? CustomerToldPrice { get; set; }
    public LensRangeType? LensRangeType { get; set; }
    public Guid? PresetCatalogueId { get; set; }
    public decimal? SphereLeft { get; set; }
    public decimal? CylinderLeft { get; set; }
    public decimal? AxisLeft { get; set; }
    public decimal? AddLeft { get; set; }
    public decimal? SphereRight { get; set; }
    public decimal? CylinderRight { get; set; }
    public decimal? AxisRight { get; set; }
    public decimal? AddRight { get; set; }
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }
    public decimal? PupilDistanceMm { get; set; }
    public int? PresetPupilDistanceBucket { get; set; }
    public bool ChildrensFrame { get; set; }
    public Guid? CoatingPreferenceRefId { get; set; }

    /// <summary>Whether this Lead placed a custom order for its lens (ADR-0008).</summary>
    public bool OrderFromDotGlasses { get; set; }

    /// <summary>The Coating set ordered with the lens; empty for a Lead that didn't order.</summary>
    public List<Guid> CoatingRefIds { get; set; } = [];

    /// <summary>How far the order has got; null when there is none.</summary>
    public CustomOrderStatus? CustomOrderStatus { get; set; }
    public bool ConvertedFlag { get; set; }
    public Guid? SaleId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
}

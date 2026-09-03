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
    public Guid ReasonNotPurchasedRefId { get; set; }
    public string? ReasonNotPurchasedOtherText { get; set; }
    public LensRangeType? LensRangeType { get; set; }
    public Guid? PresetCatalogueId { get; set; }
    public Guid? LensOptionLeftId { get; set; }
    public Guid? LensOptionRightId { get; set; }
    public decimal? CustomSphereLeft { get; set; }
    public decimal? CustomCylinderLeft { get; set; }
    public decimal? CustomAxisLeft { get; set; }
    public decimal? CustomAddPowerLeft { get; set; }
    public decimal? CustomSphereRight { get; set; }
    public decimal? CustomCylinderRight { get; set; }
    public decimal? CustomAxisRight { get; set; }
    public decimal? CustomAddPowerRight { get; set; }
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }
    public decimal? PupilDistanceMm { get; set; }
    public int? PresetPupilDistanceBucket { get; set; }
    public bool ChildrensFrame { get; set; }
    public Guid? CoatingPreferenceRefId { get; set; }
    public bool ConvertedFlag { get; set; }
    public Guid? SaleId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
}

using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.Sales;

public class SaleDto
{
    public Guid Id { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
    public Guid TechnicianUserId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? SourceLeadId { get; set; }
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
    public LensRangeType LensRangeType { get; set; }
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
    public bool OrderFromDotGlasses { get; set; }
    public decimal? PupilDistanceMm { get; set; }
    public int? PresetPupilDistanceBucket { get; set; }
    public bool ChildrensFrame { get; set; }
    public Guid FrameColourRefId { get; set; }
    public string? FrameColourOtherText { get; set; }
    public FrameCoverage FrameCoverage { get; set; }
    public Guid CoatingRefId { get; set; }
    public bool HardCaseSold { get; set; }
    public Guid? HardCaseColourRefId { get; set; }
    public string? HardCaseOtherColourText { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
}

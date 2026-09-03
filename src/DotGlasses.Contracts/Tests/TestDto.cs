using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.Tests;

public class TestDto
{
    public Guid Id { get; set; }
    public string HierarchyPath { get; set; } = string.Empty;
    public Guid TechnicianUserId { get; set; }
    public int? AgeYears { get; set; }
    public Gender Gender { get; set; }
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }
    public TestOutcome Outcome { get; set; }
    public Guid? ReferralReasonRefId { get; set; }
    public string? ReferralOtherText { get; set; }
    public string? ReferralLocationFreeText { get; set; }
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
    public Guid? ConvertedToLeadId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
}

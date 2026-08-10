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
    public Guid? ConvertedToLeadId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
}

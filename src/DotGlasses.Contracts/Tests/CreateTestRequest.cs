using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.Tests;

/// <summary>
/// Id is client-generated (offline-sync outbox idempotency key), same as WidgetExample.
/// Deliberately has no HierarchyPath/TechnicianUserId fields — the server derives both from the
/// authenticated caller (see TestsController), never trusting client-submitted values for a
/// real technician's data entry. No ConvertedToLeadId either — that's set later by the
/// Lead-linking flow (see LeadService), not at Test creation. Tests stay deliberately anonymous
/// (no name/phone captured, and no Customer link — see CLAUDE.md's Phase 3 notes), unlike
/// Lead/Sale.
/// </summary>
public class CreateTestRequest
{
    public Guid Id { get; set; }
    public int? AgeYears { get; set; }
    public Gender Gender { get; set; }
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }
    public TestOutcome Outcome { get; set; }
    public Guid? ReferralReasonRefId { get; set; }
    public string? ReferralOtherText { get; set; }
    public string? ReferralLocationFreeText { get; set; }

    /// <summary>Which lens(es) this person needs — recordable whenever Outcome ==
    /// NeedsGlasses, whether or not the technician goes on to capture Lead contact details.
    /// Optional throughout, same shape as Lead's equivalent block.</summary>
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
}

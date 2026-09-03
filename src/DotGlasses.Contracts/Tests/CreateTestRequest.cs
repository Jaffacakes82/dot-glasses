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

    /// <summary>Independent of Outcome — see CreateLeadRequest/CreateSaleRequest's identical
    /// block for the full shape shared across all three.</summary>
    public bool ReferredOrTreated { get; set; }
    public Guid? ReferralReasonRefId { get; set; }
    public string? ReferralOtherText { get; set; }
    public string? ReferralLocationFreeText { get; set; }
    public bool TreatedInFacility { get; set; }
}

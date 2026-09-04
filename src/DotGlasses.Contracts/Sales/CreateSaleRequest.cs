using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.Sales;

/// <summary>
/// Id is client-generated (offline-sync outbox idempotency key). No HierarchyPath/
/// TechnicianUserId (server-derived, see TestsController). No CustomerId — server finds-or-
/// creates a Customer from FullName+PhoneNumber, same as Lead.
///
/// CoatingRefId is required for every LensRangeType now (2026-08-05 — previously ignored for
/// preset ranges, server-derived from a single forced coating per lens; see LensOption's doc
/// comment for why that was replaced). For Custom, any active Coating item is valid; for a
/// preset range, it must be one of the coatings configured as available for the chosen left-eye
/// LensOption's lens strength — see SaleService/CreateSaleRequestValidator.
/// </summary>
public class CreateSaleRequest
{
    public Guid Id { get; set; }

    /// <summary>Set if this Sale converts a Lead — see LeadsController/LeadDto.</summary>
    public Guid? SourceLeadId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public int? AgeYears { get; set; }
    public Gender Gender { get; set; }
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }

    public bool ConsentGiven { get; set; }

    /// <summary>"Referred or treated" — independently captured at creation time, same shape on
    /// Test/Lead/Sale. Not gated on any particular outcome/result.</summary>
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

    /// <summary>Required when either add power is set (two distinct powers on that eye) — see
    /// CreateSaleRequestValidator.</summary>
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }

    /// <summary>Only meaningful when LensRangeType == Custom — routes to fulfilment.</summary>
    public bool OrderFromDotGlasses { get; set; }

    /// <summary>The real inter-pupillary distance in mm — required for Custom range only.</summary>
    public decimal? PupilDistanceMm { get; set; }

    /// <summary>Coarse 0-4 PD shorthand for a preset range (0-2 when ChildrensFrame) — required
    /// for a preset range only, see Sale.PresetPupilDistanceBucket.</summary>
    public int? PresetPupilDistanceBucket { get; set; }

    public bool ChildrensFrame { get; set; }

    public Guid FrameColourRefId { get; set; }
    public string? FrameColourOtherText { get; set; }
    public FrameCoverage FrameCoverage { get; set; }

    /// <summary>Required for every LensRangeType now — see class summary.</summary>
    public Guid? CoatingRefId { get; set; }

    public bool HardCaseSold { get; set; }
    public Guid? HardCaseColourRefId { get; set; }
    public string? HardCaseOtherColourText { get; set; }
}

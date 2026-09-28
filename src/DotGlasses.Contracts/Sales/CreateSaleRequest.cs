using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.Sales;

/// <summary>
/// Id is client-generated (offline-sync outbox idempotency key). No HierarchyPath/
/// TechnicianUserId (server-derived, see TestsController). No CustomerId — server finds-or-
/// creates a Customer from FullName+PhoneNumber, same as Lead.
///
/// CoatingRefIds requires at least one entry for every LensRangeType (2026-08-05 — previously
/// ignored for lens sets, server-derived from a single forced coating per lens; see
/// LensOption's doc comment for why that was replaced; 2026-09-03 — became a set rather than a
/// single value, see ADR-0001). For Custom, any active Coating item is valid; for a lens set,
/// every entry must be one of the coatings the left eye's lens set lens (the lens in the set
/// matching SphereLeft…AddLeft and LensTypeRefId) comes in — see
/// ConsultationRules. Coating exclusions (ADR-0001) apply to the set regardless of LensRangeType;
/// pairings now belong to each lens set lens (ADR-0007).
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

    /// <summary>Which lens set, on a LensSet range. The lenses themselves are the per-eye powers
    /// and the lens type below, for both lens ranges (ADR-0007).</summary>
    public Guid? PresetCatalogueId { get; set; }

    public decimal? SphereLeft { get; set; }
    public decimal? CylinderLeft { get; set; }
    public decimal? AxisLeft { get; set; }
    public decimal? AddLeft { get; set; }
    public decimal? SphereRight { get; set; }
    public decimal? CylinderRight { get; set; }
    public decimal? AxisRight { get; set; }
    public decimal? AddRight { get; set; }

    /// <summary>One per pair; null means single vision. Required when either add power is set
    /// (two distinct powers on that eye) — see ConsultationRules. On a lens set it is the chosen
    /// lenses' own lens type.</summary>
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }

    /// <summary>Only meaningful when LensRangeType == Custom — routes to fulfilment.</summary>
    public bool OrderFromDotGlasses { get; set; }

    /// <summary>The real inter-pupillary distance in mm — required for Custom range only.</summary>
    public decimal? PupilDistanceMm { get; set; }

    /// <summary>Coarse 0-4 PD shorthand for a lens set (0-2 when ChildrensFrame) — required
    /// for a lens set only, see Sale.PresetPupilDistanceBucket.</summary>
    public int? PresetPupilDistanceBucket { get; set; }

    public bool ChildrensFrame { get; set; }

    public Guid FrameColourRefId { get; set; }
    public string? FrameColourOtherText { get; set; }
    public FrameCoverage FrameCoverage { get; set; }

    /// <summary>At least one entry required for every LensRangeType — see class summary.</summary>
    public List<Guid> CoatingRefIds { get; set; } = [];

    public bool HardCaseSold { get; set; }
    public Guid? HardCaseColourRefId { get; set; }
    public string? HardCaseOtherColourText { get; set; }
}

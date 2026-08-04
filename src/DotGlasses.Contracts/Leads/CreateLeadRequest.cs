using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.Leads;

/// <summary>
/// Id is client-generated (offline-sync outbox idempotency key). No HierarchyPath/
/// TechnicianUserId (server-derived, see TestsController's equivalent for the same reason). No
/// CustomerId — the server finds-or-creates a Customer from FullName+PhoneNumber (see
/// LeadService); the Field App doesn't need to know the Customer id ahead of time for the v1
/// exact-match flow. No ConvertedFlag/SaleId — set later by SaleService.
/// </summary>
public class CreateLeadRequest
{
    public Guid Id { get; set; }

    /// <summary>Set if this Lead is being flipped from a Test — see TestsController/TestDto.</summary>
    public Guid? SourceTestId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public int? AgeYears { get; set; }
    public Gender Gender { get; set; }
    public Guid? OccupationRefId { get; set; }
    public string? OccupationOtherText { get; set; }

    public bool ConsentGiven { get; set; }

    public Guid ReasonNotPurchasedRefId { get; set; }
    public string? ReasonNotPurchasedOtherText { get; set; }

    /// <summary>Null if this Lead carries no product preference at all (test results only).</summary>
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

    public decimal? PupilDistanceMm { get; set; }
    public bool ChildrensFrame { get; set; }

    /// <summary>Optional — some leads carry no known product preference.</summary>
    public Guid? CoatingPreferenceRefId { get; set; }
}

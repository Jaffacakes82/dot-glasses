namespace DotGlasses.Application.Dashboard;

/// <summary>Read-only aggregate backing the Admin Portal's MI Reporting Dashboard. Hierarchy
/// scoping is automatic (Test/Lead/Sale/CustomOrder/OrganisationNode all implement
/// IHierarchyScoped), so GetAsync just needs to query normally — same insight as Event
/// History/Custom Orders. Rows attributed to an OrganisationNode.IsTrainingOrg subtree are
/// explicitly excluded (per OrganisationNode's own doc comment: "excluded from MI
/// dashboards/reporting via an explicit query condition, not a global filter").
///
/// The figures themselves are DashboardCalculator's; this loads what the caller can see and
/// works out which Countries and Retailers they can filter by.
///
/// Deliberately does NOT include a "distribution by retail-point type" tile or filter — no such
/// concept exists anywhere in the domain.</summary>
public interface IDashboardQueryService
{
    /// <summary>The date range filters every aggregate by when the record was made (a custom
    /// order: when it was placed); either end may be null. The Country and Retailer narrow every
    /// figure, the trend included. The rolling 6-week trend is unaffected by the date range — it
    /// always covers the most recent 6 real-time weeks, since a "trend over time" widget doesn't
    /// make sense re-scoped to an arbitrary custom window.</summary>
    Task<DashboardSnapshot> GetAsync(DashboardFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>The figures, plus what the two organisation filters can offer this caller: only
/// Countries and Retailers their scope contains. HasNoRetailerOption is true when some retail
/// point they can see hangs directly off a Country.</summary>
public record DashboardSnapshot(
    DashboardFigures Figures,
    IReadOnlyList<DashboardOrgOption> Countries,
    IReadOnlyList<DashboardOrgOption> Retailers,
    bool HasNoRetailerOption);

public record DashboardOrgOption(Guid Id, string Name);

/// <summary>One row of a "Top performing" list. ConversionPercent is, of the Tests recorded
/// under this name, the share that reached a Sale through a Lead — never above 100.</summary>
public record DashboardRankedEntry(string Name, int Tests, int Leads, int Sales, double ConversionPercent);

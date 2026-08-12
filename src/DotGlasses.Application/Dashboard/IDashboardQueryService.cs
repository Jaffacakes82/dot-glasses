namespace DotGlasses.Application.Dashboard;

/// <summary>Read-only aggregate backing the Admin Portal's MI Reporting Dashboard. Hierarchy
/// scoping is automatic (Test/Lead/Sale/OrganisationNode all implement IHierarchyScoped), so a
/// single GetAsync just needs to query normally — same insight as Event History/Custom Orders.
/// Rows attributed to an OrganisationNode.IsTrainingOrg subtree are explicitly excluded (per
/// OrganisationNode's own doc comment: "excluded from MI dashboards/reporting via an explicit
/// query condition, not a global filter").
///
/// Deliberately does NOT include a "distribution by retail-point type" tile — no such concept
/// exists anywhere in the domain (OrganisationNode.Kind is a free-text display label with no
/// fixed taxonomy behind it), and the design mockup's Physical/Mobile Agent/Outreach categories
/// were never confirmed with the user. Deliberately has no filters or a sales-vs-conversion sort
/// toggle either — top-N lists are a fixed sort by sales volume (both were explicit 2026-08-05
/// scope decisions, see CLAUDE.md).</summary>
public interface IDashboardQueryService
{
    /// <summary>fromUtc/toUtcExclusive filter every aggregate (Tests/Leads/Sales) by
    /// CreatedAtUtc; either or both may be null for an open-ended/all-time range. The rolling
    /// 6-week ConversionTrendPercent bucket is unaffected by the range — it always covers the
    /// most recent 6 real-time weeks, since a "trend over time" widget doesn't make sense
    /// re-scoped to an arbitrary custom window.</summary>
    Task<DashboardSnapshot> GetAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, CancellationToken cancellationToken = default);
}

public record DashboardSnapshot(
    int PendingLeads,
    int TotalTests,
    int StandardSales,
    int CustomOrders,
    double TestToSaleConversionPercent,
    double NeededToSaleConversionPercent,
    int ReferralsLogged,
    /// <summary>Test-to-sale conversion %, one rolling 7-day bucket per entry, oldest first —
    /// 6 buckets covering the last 42 days.</summary>
    IReadOnlyList<int> ConversionTrendPercent,
    int GenderMalePercent,
    int GenderFemalePercent,
    IReadOnlyList<DashboardRankedEntry> TopOutlets,
    IReadOnlyList<DashboardRankedEntry> TopRetailers,
    IReadOnlyList<DashboardRankedEntry> TopCountries,
    IReadOnlyList<DashboardRankedEntry> TopTechnicians);

public record DashboardRankedEntry(string Name, int Sales, double ConversionPercent);

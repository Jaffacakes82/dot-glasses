using DotGlasses.Domain.Common;

namespace DotGlasses.Application.Reporting;

/// <summary>
/// Asking OrgTreeLookup about a *persisted row* rather than about a path already known to be well
/// formed — the one edge where the two disagree about what a bad path means.
///
/// OrgTreeLookup takes a HierarchyPath, and HierarchyPath.Parse throws. That is right for the org
/// nodes the lookup is built from: there are few of them, they are the tree itself, and a corrupt
/// one is data corruption worth surfacing rather than working around (see OrgTreeLookup's
/// constructor). It is wrong for a reporting row. IHierarchyScoped.HierarchyPath is a plain string
/// column defaulting to "", stamped server-side from a claim that is itself absent for a user with
/// no org assignment (ICurrentUserContext.HierarchyPathPrefix falls back to ""), and a reporting
/// screen reads *every* Test/Lead/Sale the caller can see — so one unparseable row would turn a
/// whole Dashboard into a 500 where it previously rendered "Unknown outlet" beside the rest of the
/// data.
///
/// These wrappers are that fallback, in one place instead of at each call site, so the module keeps
/// its strict contract (ADR-0004: the value type wraps at the application edges) and every
/// reporting screen still degrades the same way on the same bad row. An unparseable path is
/// indistinguishable from a path that names no node — both are "we cannot say", which is what the
/// Unknown* fallbacks already mean.
/// </summary>
public static class OrgTreeLookupRowExtensions
{
    public static OrganisationNodeSummary? RowOutlet(this OrgTreeLookup lookup, string? hierarchyPath) =>
        RowPath(hierarchyPath) is { } path ? lookup.FindOutlet(path) : null;

    public static string RowOutletName(this OrgTreeLookup lookup, string? hierarchyPath) =>
        RowPath(hierarchyPath) is { } path ? lookup.OutletName(path) : OrgTreeLookup.UnknownOutlet;

    public static string RowCountryName(this OrgTreeLookup lookup, string? hierarchyPath) =>
        RowPath(hierarchyPath) is { } path ? lookup.CountryName(path) : OrgTreeLookup.UnknownCountry;

    public static RetailerResolution RowRetailer(this OrgTreeLookup lookup, string? hierarchyPath) =>
        RowPath(hierarchyPath) is { } path ? lookup.ResolveRetailer(path) : RetailerResolution.Unknown;

    public static string RowRetailerName(this OrgTreeLookup lookup, string? hierarchyPath) =>
        lookup.RowRetailer(hierarchyPath).Name;

    /// <summary>An unparseable path is not under a training org — the same answer the raw prefix
    /// match gave, and the safe one either way: Dashboard aggregates exclude training rows, so
    /// guessing "yes" here would silently drop real data.</summary>
    public static bool IsRowUnderTrainingOrg(this OrgTreeLookup lookup, string? hierarchyPath) =>
        RowPath(hierarchyPath) is { } path && lookup.IsUnderTrainingOrg(path);

    private static HierarchyPath? RowPath(string? hierarchyPath) =>
        HierarchyPath.TryParse(hierarchyPath, out var path) ? path : null;
}

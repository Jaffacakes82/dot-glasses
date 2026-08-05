using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Reporting;

/// <summary>
/// The one sanctioned way to look outside the caller's hierarchy scope (e.g. an org-wide
/// report). Every other query in the codebase goes through the normal repositories and is
/// subject to the hierarchy-scoping global query filter — do not call .IgnoreQueryFilters()
/// ad hoc elsewhere. Callers must still be gated by an RBAC policy (e.g. "can view org-wide
/// reports") since this service intentionally bypasses data scoping, not authorization.
/// </summary>
public interface IUnscopedReportQueryService
{
    Task<IReadOnlyList<WidgetExample>> GetAllWidgetExamplesUnscopedAsync(CancellationToken cancellationToken = default);

    /// <summary>Every org node's Id + HierarchyPath, ignoring the hierarchy-scoping filter.
    /// Needed by anything that must look "upward" from the caller (e.g.
    /// PresetCatalogueQueryService resolving which ancestor orgs a catalogue is assigned to) —
    /// the standard filter only ever shows a caller their own subtree, so a plain scoped query
    /// against OrganisationNodes silently returns nothing for ancestor lookups.</summary>
    Task<IReadOnlyList<OrganisationNodePath>> GetOrganisationNodePathsUnscopedAsync(CancellationToken cancellationToken = default);

    /// <summary>Every org node's Id/Name/Level/HierarchyPath/IsTrainingOrg, ignoring the
    /// hierarchy-scoping filter — a superset of GetOrganisationNodePathsUnscopedAsync for callers
    /// that need to resolve a display name or ancestor level, not just match a path prefix (e.g.
    /// a Dashboard/Event-History-style report resolving "which outlet/retailer/country" for a
    /// caller who may be scoped well below those ancestors and so could never see them via a
    /// plain scoped query).</summary>
    Task<IReadOnlyList<OrganisationNodeSummary>> GetOrganisationNodesUnscopedAsync(CancellationToken cancellationToken = default);
}

public record OrganisationNodePath(Guid Id, string HierarchyPath);

public record OrganisationNodeSummary(Guid Id, string Name, OrganisationLevel Level, string HierarchyPath, bool IsTrainingOrg);

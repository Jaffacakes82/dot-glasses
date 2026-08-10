using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Organisations;

/// <summary>Admin-only org-tree management — backs the Admin Portal's Organisations screen.
/// Reading needs no special handling: OrganisationNode implements IHierarchyScoped, so a plain
/// scoped query already returns exactly "the caller's own node + everything below it." Writing
/// a new node's HierarchyPath is the one place that genuinely needs to look outside the caller's
/// own scope (a new path segment must be globally unique across the whole tree, not just the
/// caller's visible subtree) — done via IUnscopedReportQueryService, the sanctioned way to do
/// that, not an ad hoc unscoped query here.</summary>
public interface IOrganisationAdminService
{
    /// <summary>Every node visible to the caller (hierarchy-scoped automatically).</summary>
    Task<IReadOnlyList<OrganisationAdminNode>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>True if parentLevel/childLevel is a valid parent-child pairing: Dgi's only
    /// child level is Country; Country/Intermediate's child level is Intermediate or
    /// RetailPoint; RetailPoint has no valid child level at all.</summary>
    bool IsValidChildLevel(OrganisationLevel parentLevel, OrganisationLevel childLevel);

    /// <summary>Mints a new globally-unique HierarchyPath segment under parentId. Known
    /// simplification: read-current-max-then-increment has a small race window under concurrent
    /// creates — acceptable for an infrequent, admin-only action (see CLAUDE.md).</summary>
    Task<OrganisationAdminNode> CreateChildAsync(Guid parentId, string name, OrganisationLevel level, string? kind, CancellationToken cancellationToken = default);

    /// <summary>IsTrainingOrg has no Level restriction — any node can be flagged.</summary>
    Task SetTrainingOrgFlagAsync(Guid id, bool isTrainingOrg, CancellationToken cancellationToken = default);
}

public record OrganisationAdminNode(Guid Id, Guid? ParentId, string Name, OrganisationLevel Level, string? Kind, string HierarchyPath, bool IsTrainingOrg);

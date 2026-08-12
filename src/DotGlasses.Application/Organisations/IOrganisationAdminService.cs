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
    /// <summary>Every active node visible to the caller (hierarchy-scoped automatically).</summary>
    Task<IReadOnlyList<OrganisationAdminNode>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Deactivated nodes within the caller's own scope. The standard global query
    /// filter combines soft-delete and hierarchy scoping into one AND'd expression with no way to
    /// bypass just one half (see DotGlassesDbContext), so this queries with IgnoreQueryFilters()
    /// and re-applies the hierarchy prefix check manually via ICurrentUserContext —
    /// same manual-prefix-filter precedent as IUserAdminService.ListAsync (ApplicationUser isn't
    /// IHierarchyScoped either, for a different reason, but the technique is identical).</summary>
    Task<IReadOnlyList<OrganisationAdminNode>> ListDeactivatedAsync(CancellationToken cancellationToken = default);

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

    Task RenameAsync(Guid id, string name, CancellationToken cancellationToken = default);

    /// <summary>Reuses OrganisationNode's existing ISoftDeletable fields (IsDeleted), already
    /// wired into the global query filter — the same mechanism every other IHierarchyScoped/
    /// ISoftDeletable entity uses, no new column needed. Deactivating sets IsDeleted = true,
    /// which drops the node out of ListAsync and every other scoped query immediately; historical
    /// Test/Lead/Sale rows still resolve its name via IUnscopedReportQueryService (IgnoreQueryFilters),
    /// same as a retired ReferenceDataItem. Deactivating a node with active (non-deleted) children
    /// throws — deactivate the children first, rather than silently orphaning them under a node
    /// that no longer appears in any admin's tree.</summary>
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

public record OrganisationAdminNode(Guid Id, Guid? ParentId, string Name, OrganisationLevel Level, string? Kind, string HierarchyPath, bool IsTrainingOrg, bool IsActive);

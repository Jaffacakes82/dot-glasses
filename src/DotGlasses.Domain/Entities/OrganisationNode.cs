using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// Self-referencing org hierarchy tree (DGI root -> Country -> arbitrary-depth Intermediate tiers
/// -> RetailPoint leaves). HierarchyPath is this node's own materialized path, so it doubles as
/// the IHierarchyScoped value: a viewer's subtree query ("which rows can I see") applied to this
/// entity itself becomes "which orgs can I see", using the same global query filter as every
/// other scoped entity.
/// </summary>
public class OrganisationNode : IAuditable, ISoftDeletable, IHierarchyScoped
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public OrganisationLevel Level { get; set; }

    public string HierarchyPath { get; set; } = string.Empty;

    /// <summary>Excluded from MI dashboards/reporting via an explicit query condition, not a
    /// global filter — Admins still need to see and edit training orgs to clean them up.</summary>
    public bool IsTrainingOrg { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>Shared by every organisation deactivated in one action — the one chosen and
    /// everything active beneath it at the time — so reactivating restores exactly that group and
    /// leaves alone anything beneath it that was deactivated separately. Null while active.</summary>
    public Guid? DeactivationGroupId { get; set; }
}

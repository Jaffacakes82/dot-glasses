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

    /// <summary>Free-text display label only (e.g. "Distributor", "Standalone") — no business
    /// rule keys off this; only Level does.</summary>
    public string? Kind { get; set; }

    public string HierarchyPath { get; set; } = string.Empty;

    /// <summary>Excluded from MI dashboards/reporting via an explicit query condition, not a
    /// global filter — Admins still need to see and edit training orgs to clean them up.</summary>
    public bool IsTrainingOrg { get; set; }

    /// <summary>Only meaningful when Level == Country; enforced in the Application layer, not a
    /// DB constraint. Determines whether the Custom Order flow appears on the Field App for
    /// retail points under this country.</summary>
    public bool CanHandleCustomOrders { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

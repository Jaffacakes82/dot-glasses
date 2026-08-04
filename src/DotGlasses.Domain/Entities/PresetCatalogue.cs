using DotGlasses.Domain.Common;

namespace DotGlasses.Domain.Entities;

/// <summary>
/// Admin-configurable lens/coating collection (e.g. "6-Lens Set"). OwningOrgNodeId must be a Dgi
/// or Country node (enforced in the future Application layer, not here).
///
/// Deliberately does NOT implement IHierarchyScoped: catalogues are created above and must be
/// visible below (a Country's catalogue needs to be usable at every RetailPoint beneath it) —
/// the opposite direction from the standard global query filter, which assumes the viewer is
/// above the data (entity.HierarchyPath.StartsWith(viewerPrefix)). "Which catalogues can this
/// retail point use" is a bespoke query over PresetCatalogueAssignment, not this filter.
/// </summary>
public class PresetCatalogue : IAuditable, ISoftDeletable
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid OwningOrgNodeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

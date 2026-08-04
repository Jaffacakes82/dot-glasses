namespace DotGlasses.Domain.Entities;

/// <summary>
/// Explicit "assign this catalogue to a sub-organisation" record — the mechanism DGI/Country
/// admins use to cascade a PresetCatalogue down their tree. An assignment at any org node makes
/// the catalogue available to that node and everything beneath it: "which catalogues can org X
/// use" is `assignment.OrgNode.HierarchyPath` being a prefix of `X.HierarchyPath`, resolved as a
/// bespoke Application-layer query, not the standard IHierarchyScoped filter (see PresetCatalogue).
/// </summary>
public class PresetCatalogueAssignment
{
    public Guid Id { get; set; }

    public Guid PresetCatalogueId { get; set; }

    public Guid OrgNodeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

namespace DotGlasses.Web.Models;

public record CataloguesIndexViewModel(
    IReadOnlyList<CatalogueCard> Catalogues,
    IReadOnlyList<(Guid Id, string Label)> AllLensStrengths,
    IReadOnlyList<(Guid Id, string Label)> ActiveCoatings,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> AvailableCoatingsByLensStrength,
    IReadOnlyList<(Guid Id, string Name)> AssignableOrgs);

public record CatalogueCard(Guid Id, string Name, string? Description, string? RangeDescription, IReadOnlyList<LensOptionCard> LensOptions, int AssignedOrgCount);

public record LensOptionCard(Guid Id, Guid LensStrengthRefId, string Label, int SortOrder);

public class CreateCatalogueRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RangeDescription { get; set; }
}

public class UpdateCatalogueRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RangeDescription { get; set; }
}

public class AddLensOptionRequest
{
    public Guid CatalogueId { get; set; }
    public Guid LensStrengthRefId { get; set; }
}

public class AssignCataloguesRequest
{
    public Guid OrgNodeId { get; set; }
    public List<Guid> CatalogueIds { get; set; } = [];
}

public class SetCoatingAvailabilityRequest
{
    public Guid LensStrengthRefId { get; set; }
    public Guid CoatingRefId { get; set; }
    public bool Available { get; set; }
}

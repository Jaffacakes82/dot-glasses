using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public class CreateChildOrganisationRequest
{
    public Guid ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public OrganisationLevel Level { get; set; }
    public string? Kind { get; set; }
}

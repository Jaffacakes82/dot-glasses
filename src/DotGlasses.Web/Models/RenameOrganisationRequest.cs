namespace DotGlasses.Web.Models;

public class RenameOrganisationRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

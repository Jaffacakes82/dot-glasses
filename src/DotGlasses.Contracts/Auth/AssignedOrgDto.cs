namespace DotGlasses.Contracts.Auth;

public class AssignedOrgDto
{
    public Guid OrgNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

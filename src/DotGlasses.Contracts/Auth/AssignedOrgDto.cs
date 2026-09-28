namespace DotGlasses.Contracts.Auth;

/// <summary>One of the caller's eligible locations (an active retail point they are directly
/// assigned to), as GET /api/v1/auth/my-orgs lists them.</summary>
public class AssignedOrgDto
{
    public Guid OrgNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>True for the caller's token's current location.</summary>
    public bool IsActive { get; set; }
}

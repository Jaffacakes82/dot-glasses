namespace DotGlasses.Application.Common;

/// <summary>
/// Claim type names shared between DotGlasses.Web (issues them, both for cookie sign-in and
/// JWT) and DotGlasses.Infrastructure's CurrentUserContext (reads them). Kept here, not in
/// Infrastructure, so Web doesn't need to reference Infrastructure just to know a claim name.
/// </summary>
public static class DotGlassesClaimTypes
{
    public const string OrgNodeId = "dotglasses:org_node_id";
    public const string HierarchyPath = "dotglasses:hierarchy_path";
}

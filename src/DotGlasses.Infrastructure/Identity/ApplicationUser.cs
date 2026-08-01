using Microsoft.AspNetCore.Identity;

namespace DotGlasses.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? OrgNodeId { get; set; }

    /// <summary>Materialized path of this user's org node, e.g. "/1/4/". Copied onto the
    /// cookie/JWT HierarchyPath claim at sign-in so CurrentUserContext never needs a DB round
    /// trip to know it.</summary>
    public string HierarchyPath { get; set; } = string.Empty;
}

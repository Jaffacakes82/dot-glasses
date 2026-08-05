using DotGlasses.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace DotGlasses.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid? OrgNodeId { get; set; }

    /// <summary>Materialized path of this user's org node, e.g. "/1/4/". Copied onto the
    /// cookie/JWT HierarchyPath claim at sign-in so CurrentUserContext never needs a DB round
    /// trip to know it.</summary>
    public string HierarchyPath { get; set; } = string.Empty;

    /// <summary>Denormalized OrganisationNode.Level of OrgNodeId, same rationale as
    /// HierarchyPath — kept in sync whenever a user's org assignment changes, copied onto the
    /// OrgLevel claim at sign-in so RBAC's OrgLevelRequirement never needs a DB round trip.</summary>
    public OrganisationLevel? OrgLevel { get; set; }

    /// <summary>Stamped on every successful sign-in, both the MVC cookie path (AccountController)
    /// and the API JWT path (AuthController) — a RetailPoint User almost never touches the Admin
    /// Portal, so only stamping the cookie path would leave this permanently null for most users.</summary>
    public DateTimeOffset? LastLoginUtc { get; set; }

    /// <summary>Nullable — the three DevUserSeeder dev accounts predate this field and have
    /// none; User Directory falls back to UserName/Email for display when absent.</summary>
    public string? FullName { get; set; }
}

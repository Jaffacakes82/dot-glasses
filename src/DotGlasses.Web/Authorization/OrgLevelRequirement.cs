using DotGlasses.Application.Common;
using DotGlasses.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace DotGlasses.Web.Authorization;

/// <summary>
/// Role membership AND the acting user's highest assigned level is at or above (numerically
/// &lt;=) minimumLevel (Dgi=0 &lt; Country=1 &lt; Intermediate=2 &lt; RetailPoint=3). Backs the
/// level-only policies that don't need a specific target resource — e.g. "Custom Orders visible
/// to Country and above" (see AuthorizationPolicies).
///
/// Highest *assigned* level, not the level of any one org: a DGI admin who is also assigned to a
/// retail point is a DGI admin (ADR-0006). Role and level both come from ICurrentUserContext,
/// which reads them from the database on every request — never from the sign-in claims, so a
/// role change or a removed assignment bites on the user's next request. Depends only on
/// ICurrentUserContext (Application), never DotGlassesDbContext directly — see CLAUDE.md's Clean
/// Architecture rule that only Web's Program.cs and AppHost may reference Infrastructure.
/// </summary>
public class OrgLevelRequirement(OrganisationLevel minimumLevel, params string[] allowedRoles) : IAuthorizationRequirement
{
    public OrganisationLevel MinimumLevel { get; } = minimumLevel;

    public IReadOnlyCollection<string> AllowedRoles { get; } = allowedRoles;
}

public class OrgLevelAuthorizationHandler(ICurrentUserContext currentUser) : AuthorizationHandler<OrgLevelRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OrgLevelRequirement requirement)
    {
        if (currentUser.Role is { } role &&
            requirement.AllowedRoles.Contains(role) &&
            currentUser.HighestLevel is { } level &&
            level <= requirement.MinimumLevel)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

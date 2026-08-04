using DotGlasses.Application.Common;
using DotGlasses.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace DotGlasses.Web.Authorization;

/// <summary>
/// Role membership AND the acting user's own org Level is at or above (numerically &lt;=)
/// minimumLevel (Dgi=0 &lt; Country=1 &lt; Intermediate=2 &lt; RetailPoint=3). Backs the level-only
/// policies that don't need a specific target resource — e.g. "Custom Orders visible to Country
/// and above" (see AuthorizationPolicies). Depends only on ICurrentUserContext (Application),
/// never DotGlassesDbContext directly — see CLAUDE.md's Clean Architecture rule that only Web's
/// Program.cs and AppHost may reference Infrastructure.
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
        if (requirement.AllowedRoles.Any(context.User.IsInRole) &&
            currentUser.OrgLevel is { } level &&
            level <= requirement.MinimumLevel)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

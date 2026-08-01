using Microsoft.AspNetCore.Authorization;

namespace DotGlasses.Web.Authorization;

/// <summary>
/// RBAC example gating WidgetExample creation (brief 3.2b) — entirely separate from the
/// hierarchy-scoping global query filter in DotGlasses.Infrastructure, which governs which
/// rows a user can see at all, not what they can do with them. [OPEN]: the real permission
/// matrix (e.g. "a Manager may only create at or below their own node") is pending the CEO
/// conversation; this only checks role membership as the placeholder pattern to extend.
/// </summary>
public class MinimumRoleRequirement(params string[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = allowedRoles;
}

public class MinimumRoleAuthorizationHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MinimumRoleRequirement requirement)
    {
        if (requirement.AllowedRoles.Any(context.User.IsInRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

using Microsoft.AspNetCore.Authorization;

namespace DotGlasses.Web.Authorization;

/// <summary>
/// RBAC example gating WidgetExample creation (brief 3.2b) — entirely separate from the
/// hierarchy-scoping global query filter in DotGlasses.Infrastructure, which governs which
/// rows a user can see at all, not what they can do with them. Only checks role membership,
/// deliberately not node-scoped — WidgetExample stays the architectural reference pattern, not
/// a real feature that needs resource-based checks (see HierarchyDescendantRequirement for
/// that pattern, used by the real entities).
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

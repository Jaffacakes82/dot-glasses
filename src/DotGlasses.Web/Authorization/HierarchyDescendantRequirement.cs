using DotGlasses.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace DotGlasses.Web.Authorization;

/// <summary>
/// Resource-based: role membership AND the target's HierarchyPath starts with the acting user's
/// own HierarchyPathPrefix (target is at/below the actor's node). Backs
/// AuthorizationPolicies.ManageUsersInScope/ManageOrgInScope — call via
/// `AuthorizeAsync(User, targetHierarchyPath, policyName)`.
///
/// Deliberately does NOT also check the target user's role: a Manager can manage any role at/
/// below their node, including other Admins in a child org (2026-08-04 decision) — that's
/// different from MinimumRoleRequirement's "who can call this at all" check, which still applies
/// via AllowedRoles here.
///
/// Not yet wired to a controller action — UserDirectoryController/OrganisationsController don't
/// operate on real per-user/per-org resources yet (still static placeholder data). Ships now so
/// it's ready when they do.
/// </summary>
public class HierarchyDescendantRequirement(params string[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = allowedRoles;
}

public class HierarchyDescendantAuthorizationHandler(ICurrentUserContext currentUser)
    : AuthorizationHandler<HierarchyDescendantRequirement, string>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, HierarchyDescendantRequirement requirement, string targetHierarchyPath)
    {
        if (requirement.AllowedRoles.Any(context.User.IsInRole) &&
            !string.IsNullOrEmpty(currentUser.HierarchyPathPrefix) &&
            targetHierarchyPath.StartsWith(currentUser.HierarchyPathPrefix, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace DotGlasses.Web.Authorization;

/// <summary>
/// Resource-based: role membership AND the target's HierarchyPath sits at/below *any* of the
/// acting user's scope paths — their org assignments combined (ADR-0006). Role and scope come from
/// ICurrentUserContext, read from the database on every request, never from the sign-in claims.
/// Backs
/// AuthorizationPolicies.ManageOrgInScope and the lens-set policies — call via
/// `AuthorizeAsync(User, targetHierarchyPath, policyName)`.
///
/// Deliberately does NOT also check the target user's role: an Admin can manage any role at/
/// below their node, including other Admins in a child org (2026-08-04 decision, originally
/// framed around Manager before the 2026-08-10 Manager→Admin collapse — see CLAUDE.md's Access
/// model section) — that's different from the simple "who can call this at all" role check,
/// which still applies via AllowedRoles here.
///
/// Wired to OrganisationsController (CreateChild, the two flag toggles, AssignUser, UnassignUser)
/// and CataloguesController
/// (PresetCatalogue.EditInScope against a lens set's owning org; PresetCatalogue.AssignInScope
/// against the org being assigned to).
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
        if (currentUser.Role is { } role &&
            requirement.AllowedRoles.Contains(role) &&
            HierarchyPath.TryParse(targetHierarchyPath, out var target) &&
            currentUser.ScopePaths.Any(target.IsSelfOrDescendantOf))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

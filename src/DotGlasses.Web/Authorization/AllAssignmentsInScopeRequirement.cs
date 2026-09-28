using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using Microsoft.AspNetCore.Authorization;

namespace DotGlasses.Web.Authorization;

/// <summary>The resource AllAssignmentsInScopeRequirement is checked against: every org a user is
/// (or, for an invite, is about to be) assigned to, inside the caller's scope or not.</summary>
public sealed record UserAssignments(IReadOnlyCollection<HierarchyPath> Paths);

/// <summary>
/// Resource-based, against a whole user: role membership AND <em>every one</em> of the target
/// user's org assignments sits at/below one of the acting user's scope paths (ADR-0006). Backs
/// AuthorizationPolicies.ManageUsersInScope — call via
/// `AuthorizeAsync(User, new UserAssignments(paths), policyName)`.
///
/// Stricter than HierarchyDescendantRequirement on purpose. Seeing a user needs only one of their
/// assignments in scope, but suspending them, resetting their password or changing their role
/// acts on all of their access at once — so without "all", a country admin could suspend a DGI
/// admin who also holds a retail point in that country. Adding or removing a single assignment
/// touches only that org, and stays on the per-org check (ManageOrgInScope).
///
/// A user with no assignments fails closed. Like HierarchyDescendantRequirement, it doesn't look
/// at the target's own role: an Admin can manage another Admin wholly inside their scope.
/// </summary>
public class AllAssignmentsInScopeRequirement(params string[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = allowedRoles;
}

public class AllAssignmentsInScopeAuthorizationHandler(ICurrentUserContext currentUser)
    : AuthorizationHandler<AllAssignmentsInScopeRequirement, UserAssignments>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AllAssignmentsInScopeRequirement requirement, UserAssignments target)
    {
        if (currentUser.Role is { } role &&
            requirement.AllowedRoles.Contains(role) &&
            target.Paths.Count > 0 &&
            target.Paths.All(path => currentUser.ScopePaths.Any(path.IsSelfOrDescendantOf)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

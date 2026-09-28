using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Common;

/// <summary>
/// What the current user may see and do on this request, read from the database rather than
/// from the sign-in cookie or JWT (ADR-0006) — so a removed assignment, a changed role or a
/// suspension takes effect on the user's very next request.
/// </summary>
/// <param name="ScopePaths">The request's scope: the hierarchy paths whose subtrees this request
/// can see, none nested inside another (see HierarchyPath.Outermost). On an Admin Portal request
/// these are the user's org assignments combined; empty means no rows.</param>
/// <param name="HighestLevel">The highest (numerically lowest) level among the user's org
/// assignments — what a level-gated screen checks. Null when the user has no assignment.</param>
/// <param name="Role">The user's one role, which applies across their whole scope.</param>
/// <param name="IsSuspended">True while the account is suspended; the request is then refused.</param>
public sealed record UserAccess(
    IReadOnlyList<HierarchyPath> ScopePaths,
    OrganisationLevel? HighestLevel,
    string? Role,
    bool IsSuspended)
{
    /// <summary>No scope, no level, no role — what an anonymous request, or one whose access was
    /// never loaded, is treated as. Fails closed.</summary>
    public static UserAccess None { get; } = new([], null, null, false);

    /// <summary>Builds the Admin Portal access of a user from their org assignments: the scope is
    /// every assignment combined, each subtree counted once.</summary>
    public static UserAccess FromAssignments(
        IEnumerable<(HierarchyPath Path, OrganisationLevel Level)> assignments, string? role, bool isSuspended)
    {
        var list = assignments.ToList();

        return new UserAccess(
            HierarchyPath.Outermost(list.Select(a => a.Path)),
            list.Count == 0 ? null : list.Min(a => a.Level),
            role,
            isSuspended);
    }
}

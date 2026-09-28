using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Common;

/// <summary>
/// The current user. Identity (UserId, UserName) comes from the cookie/JWT claims; everything
/// that decides access — ScopePaths, HighestLevel, Role, IsSuspended — comes from the database,
/// loaded once per request by IUserAccessLoader and memoised for it (ADR-0006). Drives both the
/// audit interceptor (who made this change) and the hierarchy-scoping global query filter (which
/// rows this user can see) in DotGlasses.Infrastructure — RBAC (what they're allowed to do) is
/// separate, and only feeds policy handlers in DotGlasses.Web, never the query filter.
/// </summary>
public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? UserName { get; }

    /// <summary>The request's scope: every row whose HierarchyPath starts with one of these is
    /// visible, and nothing else is. On the Admin Portal, the user's org assignments combined
    /// (nested ones removed); on the Field App, the token's org. Empty when the user's access was
    /// never loaded — which the filter and the permission checks treat as "nothing".</summary>
    IReadOnlyList<HierarchyPath> ScopePaths { get; }

    /// <summary>The highest (numerically lowest) level among the user's org assignments — drives
    /// RBAC's OrgLevelRequirement. Null if the user has no assignment.</summary>
    OrganisationLevel? HighestLevel { get; }

    /// <summary>The user's one role, read from the database on this request.</summary>
    string? Role { get; }

    bool IsSuspended { get; }

    IReadOnlyCollection<string> Roles { get; }

    // --- Single-org members (the old "active org", read from claims) ------------------------
    // Still read by the consumers not yet moved onto the combined scope: the Organisations tree,
    // User Directory, lens-set creation and the Field App API. Scope and permission decisions
    // must not use them.

    Guid? OrgNodeId { get; }

    /// <summary>Materialized-path prefix of the active org, e.g. "/1/4/".</summary>
    string HierarchyPathPrefix { get; }

    /// <summary>OrganisationNode.Level of the active org.</summary>
    OrganisationLevel? OrgLevel { get; }
}

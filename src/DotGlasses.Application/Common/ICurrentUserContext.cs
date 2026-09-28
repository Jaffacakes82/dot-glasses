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
    /// (nested ones removed); on the Field App, the valid current location alone. Empty when the
    /// user's access was never loaded, or a Field App request has no valid current location —
    /// which the filter and the permission checks treat as "nothing".</summary>
    IReadOnlyList<HierarchyPath> ScopePaths { get; }

    /// <summary>The highest (numerically lowest) level among the user's org assignments — drives
    /// RBAC's OrgLevelRequirement. Null if the user has no assignment.</summary>
    OrganisationLevel? HighestLevel { get; }

    /// <summary>The user's one role, read from the database on this request.</summary>
    string? Role { get; }

    bool IsSuspended { get; }

    IReadOnlyCollection<string> Roles { get; }

    /// <summary>Field App (JWT) requests only: the current location the token names, re-validated
    /// on this request — still a direct assignment, Retail Point level, active. Its Status says
    /// why when it isn't valid, and its Location names the org whenever the token names a real
    /// one, so a refusal can name it. Always NoLocation on Admin Portal requests. Read this for
    /// anything the Field App records.</summary>
    CurrentLocationCheck CurrentLocation { get; }
}

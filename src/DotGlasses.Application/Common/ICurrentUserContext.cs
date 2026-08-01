namespace DotGlasses.Application.Common;

/// <summary>
/// The current user, sourced from claims in DotGlasses.Web and from locally cached auth state
/// in DotGlasses.App. Drives both the audit interceptor (who made this change) and the
/// hierarchy-scoping global query filter (which rows this user can see) in
/// DotGlasses.Infrastructure — RBAC (what they're allowed to do) is separate, see Roles here
/// only feeds policy handlers in DotGlasses.Web, never the query filter.
/// </summary>
public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? UserName { get; }
    Guid? OrgNodeId { get; }

    /// <summary>Materialized-path prefix, e.g. "/1/4/". Matches this user's org node and everything below it.</summary>
    string HierarchyPathPrefix { get; }

    IReadOnlyCollection<string> Roles { get; }
}

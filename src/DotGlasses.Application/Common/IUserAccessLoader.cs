using System.Security.Claims;

namespace DotGlasses.Application.Common;

/// <summary>
/// Reads a signed-in user's <see cref="UserAccess"/> from the database and memoises it for the
/// rest of the request, where ICurrentUserContext and the hierarchy query filter read it. Called
/// once per request by each authentication scheme's validation event (DotGlasses.Web's
/// Program.cs) — never cached across requests: Container Apps runs several replicas, and a
/// cross-request cache would reintroduce the delay ADR-0006 removes.
///
/// Returns null when the user no longer exists. The caller decides what a suspended or missing
/// user means for its scheme (sign out, or 401).
/// </summary>
public interface IUserAccessLoader
{
    /// <summary>Admin Portal (cookie) requests: the scope is the union of the user's org
    /// assignments.</summary>
    Task<UserAccess?> LoadForAdminPortalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>Field App (JWT) requests: role, level and suspension come from the database like
    /// the Admin Portal's, but the scope stays the token's own org for now.</summary>
    Task<UserAccess?> LoadForFieldAppAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}

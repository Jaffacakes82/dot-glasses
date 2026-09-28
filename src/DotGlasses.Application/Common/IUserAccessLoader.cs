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
    /// the Admin Portal's, but the scope is the token's current location alone — and only once it
    /// has been re-validated (see <see cref="CurrentLocationCheck.Of"/>). An invalid location
    /// leaves the scope empty rather than failing the request.</summary>
    Task<UserAccess?> LoadForFieldAppAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>The user's <b>eligible locations</b> — the active retail points they are directly
    /// assigned to — by name. What the Field App may choose from at sign-in and when switching,
    /// and what "my orgs" lists. Not memoised: sign-in has no request user yet.</summary>
    Task<IReadOnlyList<CurrentLocation>> ListEligibleLocationsAsync(Guid userId, CancellationToken cancellationToken = default);
}

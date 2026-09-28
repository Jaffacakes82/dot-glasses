using DotGlasses.Application.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;

namespace DotGlasses.Web.Auth;

/// <summary>
/// The per-request recheck ADR-0006 asks for, one event per authentication scheme. Each loads
/// the user's access (assignments, role, suspension) from the database — which also memoises it
/// for the rest of the request, where the scope filter and the permission checks read it — and
/// refuses a suspended or deleted user outright. Nothing about access is taken from the cookie or
/// the token beyond who the user is, so a removed assignment, a changed role or a suspension
/// takes effect on the user's very next request instead of when the cookie refreshes (up to 30
/// minutes) or the token expires (up to 60).
/// </summary>
public static class AccessRecheck
{
    /// <summary>Cookie (Admin Portal): a suspended or deleted user is signed out, and the
    /// request continues unauthenticated — so an [Authorize] screen challenges it to the sign-in
    /// page, the same as an expired session. Chains to Identity's own security-stamp validator
    /// afterwards, which still handles its separate job (a password change signing out other
    /// sessions).</summary>
    public static async Task ValidateCookieAsync(
        CookieValidatePrincipalContext context, Func<CookieValidatePrincipalContext, Task> next)
    {
        var loader = context.HttpContext.RequestServices.GetRequiredService<IUserAccessLoader>();
        var access = context.Principal is { } principal
            ? await loader.LoadForAdminPortalAsync(principal, context.HttpContext.RequestAborted)
            : null;

        if (access is null || access.IsSuspended)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        await next(context);
    }

    /// <summary>JWT (Field App): a suspended or deleted user's token is refused — 401 — however
    /// long it has left to run. The Field App's outbox already treats a 401 as a rejection. The
    /// token's current location is re-validated by the same load, but an invalid one does not
    /// fail the token: it leaves the request with no scope, and the create endpoints refuse.</summary>
    public static async Task ValidateTokenAsync(TokenValidatedContext context)
    {
        var loader = context.HttpContext.RequestServices.GetRequiredService<IUserAccessLoader>();
        var access = context.Principal is { } principal
            ? await loader.LoadForFieldAppAsync(principal, context.HttpContext.RequestAborted)
            : null;

        if (access is null || access.IsSuspended)
        {
            context.Fail("The account is suspended or no longer exists.");
        }
    }
}

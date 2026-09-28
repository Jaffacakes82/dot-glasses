using System.Text.Encodings.Web;
using DotGlasses.Application.Common;
using DotGlasses.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotGlasses.Web.Tests.DomainRejections;

/// <summary>
/// Stands in for the Identity application cookie so the Admin Portal's server-rendered screens
/// can be driven over HTTP without a real sign-in. The user id comes off a request header rather
/// than shared state, so each HttpClient in a test class can act as a different user.
///
/// Only the *sign-in* step is faked, and faithfully: the account is a real row, its claims are
/// stamped by the real claims-principal factory (exactly what the cookie would carry), and its
/// access is loaded from the database by the same IUserAccessLoader call the cookie's per-request
/// validation event makes. Every policy, resource-based check and the hierarchy query filter then
/// run exactly as they do in production.
///
/// Deliberately *not* a copy of that event's refusal of suspended or deleted users: a duplicate
/// here could keep passing while the real one regressed. Those cases are covered only through the
/// real Identity cookie (AccessControl/CombinedScopeTests, via AccessControlFixture's real
/// sign-in form), where AccessRecheck and its chaining in Program.cs actually run.
/// </summary>
public class AdminPortalTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AdminPortalTest";

    public const string UserIdHeader = "X-Test-UserId";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId))
        {
            return AuthenticateResult.NoResult();
        }

        var services = Context.RequestServices;
        var user = await services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthenticateResult.Fail("No such user.");
        }

        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>().CreateAsync(user);
        await services.GetRequiredService<IUserAccessLoader>().LoadForAdminPortalAsync(principal);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}

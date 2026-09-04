using System.Security.Claims;
using System.Text.Encodings.Web;
using DotGlasses.Application.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotGlasses.Web.Tests.DomainRejections;

/// <summary>
/// Stands in for the Identity application cookie so the Admin Portal's server-rendered screens
/// can be driven over HTTP without a real sign-in. The claims come off request headers rather
/// than shared state, so each HttpClient in a test class can act as a different user.
///
/// Only the *authentication* step is faked — every policy, resource-based check and the
/// hierarchy query filter still run against these claims exactly as they do in production, which
/// is what makes the screen tests below meaningful.
/// </summary>
public class AdminPortalTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AdminPortalTest";

    public const string UserIdHeader = "X-Test-UserId";
    public const string HierarchyPathHeader = "X-Test-HierarchyPath";
    public const string OrgLevelHeader = "X-Test-OrgLevel";
    public const string OrgNodeIdHeader = "X-Test-OrgNodeId";
    public const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "admin-portal-test"),
            new(ClaimTypes.Role, Header(RoleHeader) ?? RoleNames.Admin),
            new(DotGlassesClaimTypes.HierarchyPath, Header(HierarchyPathHeader) ?? "/1/"),
            new(DotGlassesClaimTypes.OrgLevel, Header(OrgLevelHeader) ?? "Dgi"),
        ];

        if (Header(OrgNodeIdHeader) is { } orgNodeId)
        {
            claims.Add(new Claim(DotGlassesClaimTypes.OrgNodeId, orgNodeId));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    private string? Header(string name) =>
        Request.Headers.TryGetValue(name, out var value) ? value.ToString() : null;
}

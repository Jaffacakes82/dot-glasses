using System.Text.RegularExpressions;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.DomainRejections;

/// <summary>
/// CustomWebApplicationFactory plus a stand-in for the Identity application cookie, so the
/// server-rendered Admin Portal screens are reachable over HTTP. Left as a subclass rather than
/// a change to CustomWebApplicationFactory — the JWT-only API tests have no use for it.
/// </summary>
public class AdminPortalFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // AddIdentity points the default schemes at the application cookie; this runs after
            // it, so the last-registered Configure<AuthenticationOptions> wins. The API
            // controllers name JwtBearer explicitly and are unaffected.
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = AdminPortalTestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = AdminPortalTestAuthenticationHandler.SchemeName;
                    options.DefaultScheme = AdminPortalTestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, AdminPortalTestAuthenticationHandler>(
                    AdminPortalTestAuthenticationHandler.SchemeName, _ => { });
        });
    }

    /// <summary>A client acting as a freshly created Admin assigned to the seeded org at the given
    /// level (DGI → Kenya → Kangemi Vision Centre → Outreach Post). Redirects are followed by
    /// hand so a test can assert on the 302 the rejection filter produces as well as on the page
    /// it lands on.</summary>
    public HttpClient CreateAdminClient(OrganisationLevel orgLevel = OrganisationLevel.Dgi)
    {
        var assignedOrgId = orgLevel switch
        {
            OrganisationLevel.Dgi => OrganisationSeedConfiguration.DgiId,
            OrganisationLevel.Country => OrganisationSeedConfiguration.KenyaId,
            OrganisationLevel.Intermediate => OrganisationSeedConfiguration.KenyaRetailerId,
            _ => OrganisationSeedConfiguration.KenyaRetailPointId,
        };
        var userId = Guid.NewGuid();

        Seed(db =>
        {
            var userName = $"admin-{userId:N}@test.local";

            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = userName,
                NormalizedEmail = userName.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            });
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = db.Roles.Single(r => r.Name == RoleNames.Admin).Id });
            db.UserOrgAssignments.Add(new UserOrgAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrgNodeId = assignedOrgId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        });

        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(AdminPortalTestAuthenticationHandler.UserIdHeader, userId.ToString());
        return client;
    }

    /// <summary>A client acting as a freshly created Admin with exactly the given org
    /// assignments (ADR-0006) — for scenarios CreateAdminClient's single-assignment shape can't
    /// exercise, such as a lens set's owning-org picker offering more than one choice.</summary>
    public (HttpClient Client, Guid UserId) CreateAdminClientWithAssignments(params Guid[] assignedOrgIds)
    {
        var userId = Guid.NewGuid();

        Seed(db =>
        {
            var userName = $"admin-{userId:N}@test.local";

            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = userName,
                NormalizedEmail = userName.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            });
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = db.Roles.Single(r => r.Name == RoleNames.Admin).Id });
            db.UserOrgAssignments.AddRange(assignedOrgIds.Select(orgId => new UserOrgAssignment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrgNodeId = orgId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            }));
        });

        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(AdminPortalTestAuthenticationHandler.UserIdHeader, userId.ToString());
        return (client, userId);
    }

    public void Seed(Action<DotGlassesDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        seed(dbContext);
        dbContext.SaveChanges();
    }

    /// <summary>An invited account (no password, no role) holding exactly the given org
    /// assignment — a target for the Organisations screen's un-assign action.</summary>
    public async Task<ApplicationUser> SeedUserAsync(string email, Guid assignedOrgNodeId)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = email,
        };

        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        Seed(db => db.UserOrgAssignments.Add(new UserOrgAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrgNodeId = assignedOrgNodeId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        }));
        return user;
    }

    /// <summary>Fetches an antiforgery token (and its paired cookie, which the client's own
    /// handler keeps) from any authenticated page — every screen carries the layout's sign-out
    /// form, so the token is not tied to the action under test.</summary>
    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"No antiforgery token found on {path}.");
        return match.Groups[1].Value;
    }

    public static FormUrlEncodedContent Form(string antiforgeryToken, params (string Key, string Value)[] fields)
    {
        var pairs = fields
            .Select(f => new KeyValuePair<string, string>(f.Key, f.Value))
            .Append(new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken));

        return new FormUrlEncodedContent(pairs);
    }

    /// <summary>Posts a form and follows the one redirect the rejection filter produces, so the
    /// assertion can be made against the HTML the user actually ends up looking at.</summary>
    public static async Task<(HttpResponseMessage Redirect, string LandingHtml)> PostAndFollowAsync(
        HttpClient client, string path, HttpContent form, string? referer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        if (referer is not null)
        {
            request.Headers.Referrer = new Uri(client.BaseAddress!, referer);
        }

        var redirect = await client.SendAsync(request);
        if (redirect.StatusCode != System.Net.HttpStatusCode.Found)
        {
            // Carry the body into the failure message — a 403/500 here is otherwise a bare
            // status code with no clue which check refused.
            Assert.Fail($"Expected a redirect from {path}, got {(int)redirect.StatusCode}: {await redirect.Content.ReadAsStringAsync()}");
        }

        var landing = await client.GetAsync(redirect.Headers.Location);
        landing.EnsureSuccessStatusCode();
        return (redirect, await landing.Content.ReadAsStringAsync());
    }
}

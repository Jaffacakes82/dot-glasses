using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DotGlasses.Application.Common;
using DotGlasses.Application.Users;
using DotGlasses.Contracts.Auth;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// ADR-0006: a user's Admin Portal access is the union of all their org assignments — never just
/// the lowest one, the shape of the CEO's "access drops to the lowest org" bug. The accounts come
/// from AccessControlFixture.
///
/// Every Admin Portal client here signs in through the real /Account/Login form and holds the real
/// Identity cookie, so the per-request recheck under test is the production one (AccessRecheck,
/// chained to the security-stamp validator in Program.cs) — not a test stand-in.
/// </summary>
public class CombinedScopeTests(AccessControlFixture fixture) : IClassFixture<AccessControlFixture>
{
    // The seeded outlet names as Event History renders them: Razor encodes the em dash in the
    // Kenyan outlet's name, so only the part after it is matched.
    private const string KenyanOutlet = "Outreach Post";
    private const string UgandanOutlet = "Kampala Outlet";

    [Fact]
    public async Task AnAdminAssignedToDgiAndARetailPoint_ReachesEveryDgiLevelScreen()
    {
        var client = await fixture.SignInAsync(AccessControlFixture.DgiAndOutletAdmin);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ReferenceData")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Catalogues")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/CustomOrders")).StatusCode);
    }

    [Fact]
    public async Task AnAdminAssignedToACountryAndARetailPoint_ReachesTheCountryLevelScreens()
    {
        var client = await fixture.SignInAsync(AccessControlFixture.CountryAndOutletAdmin);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/CustomOrders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Catalogues")).StatusCode);
    }

    [Fact]
    public async Task AnAdminAssignedToDgiAndARetailPoint_SeesDgiWideData()
    {
        SeedTest(AccessControlFixture.SecondCountryRetailPointPath);
        var client = await fixture.SignInAsync(AccessControlFixture.DgiAndOutletAdmin);

        var html = await client.GetStringAsync("/EventHistory?tab=tests");

        // A test in another country entirely — reachable only through the DGI assignment.
        Assert.Contains(UgandanOutlet, html);
    }

    [Fact]
    public async Task AnAdminAssignedToTwoCountries_SeesBothCountriesDataTogether()
    {
        SeedTest(OrganisationSeedConfiguration.KenyaRetailPointPath);
        SeedTest(AccessControlFixture.SecondCountryRetailPointPath);
        var client = await fixture.SignInAsync(AccessControlFixture.TwoCountriesAdmin);

        var html = await client.GetStringAsync("/EventHistory?tab=tests");

        Assert.Contains(KenyanOutlet, html);
        Assert.Contains(UgandanOutlet, html);
    }

    [Fact]
    public async Task ARecordCoveredByTwoOfAUsersAssignments_IsCountedOnceOnTheDashboard()
    {
        // The retail point sits under both of this admin's assignments (DGI and itself).
        SeedTest(OrganisationSeedConfiguration.KenyaRetailPointPath);

        // A DGI-only admin has exactly the same scope with no overlap, so is the independent
        // answer for what the total should be — the overlapping admin must neither double the
        // retail point's tests nor fall back to seeing only them.
        var expected = TotalTests(await (await fixture.SignInAsync(AccessControlFixture.DgiAdmin)).GetStringAsync("/"));
        var actual = TotalTests(await (await fixture.SignInAsync(AccessControlFixture.DgiAndOutletAdmin)).GetStringAsync("/"));

        Assert.True(expected > 0);
        Assert.Equal(expected, actual);
    }

    // --- Rechecked on every request -----------------------------------------------------------
    // Each change below is made straight in the database, behind the application's back, and the
    // same signed-in client is reused — so the only thing that can carry the change into the next
    // request is the server re-reading access, not a fresh sign-in or a refreshed cookie.

    [Fact]
    public async Task RemovingAnAssignment_ShrinksTheScopeOnTheUsersNextRequest()
    {
        SeedTest(AccessControlFixture.SecondCountryRetailPointPath);
        var (userName, userId) = await fixture.CreateAccountAsync(
            RoleNames.Admin, OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.DgiId);
        var client = await fixture.SignInAsync(userName);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ReferenceData")).StatusCode);
        Assert.Contains(UgandanOutlet, await client.GetStringAsync("/EventHistory?tab=tests"));

        await ChangeInDatabaseAsync(db => db.UserOrgAssignments.RemoveRange(
            db.UserOrgAssignments.Where(a => a.UserId == userId && a.OrgNodeId == OrganisationSeedConfiguration.DgiId)));

        AssertAccessDenied(await client.GetAsync("/ReferenceData"));
        Assert.DoesNotContain(UgandanOutlet, await client.GetStringAsync("/EventHistory?tab=tests"));
    }

    [Fact]
    public async Task ARoleChange_IsReflectedOnTheUsersNextRequest()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.Admin, OrganisationSeedConfiguration.DgiId);
        var client = await fixture.SignInAsync(userName);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ReferenceData")).StatusCode);

        await SetRoleInDatabaseAsync(userId, RoleNames.User);
        AssertAccessDenied(await client.GetAsync("/ReferenceData"));

        // And back again: granting is as immediate as withdrawing.
        await SetRoleInDatabaseAsync(userId, RoleNames.Admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ReferenceData")).StatusCode);
    }

    [Fact]
    public async Task ASuspendedUser_IsSentToSignInOnTheirNextAdminPortalRequest()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.Admin, OrganisationSeedConfiguration.KenyaId);
        var client = await fixture.SignInAsync(userName);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        await SuspendInDatabaseAsync(userId);

        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/Account/Login", RedirectPath(response), StringComparer.OrdinalIgnoreCase);

        // Signed out, not just turned away once: the next request is refused the same way.
        Assert.Equal("/Account/Login", RedirectPath(await client.GetAsync("/EventHistory")), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ASuspendedUser_GetsUnauthorizedOnTheirNextFieldAppRequest()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId);
        var client = await SignInToFieldAppAsync(userName);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/preset-catalogues")).StatusCode);

        await SuspendInDatabaseAsync(userId);

        // The token itself is still well within its lifetime.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/preset-catalogues")).StatusCode);
    }

    [Fact]
    public async Task AUserLockedOutBySomeoneElsesFailedSignIns_IsNotTreatedAsSuspended()
    {
        // Sign-in locks an account for a few minutes after repeated wrong passwords. Anyone who
        // knows a username can trigger that, so it must not sign the real user out of a live
        // session or 401 their Field App token — a 401 is terminal for a queued offline record.
        var (userName, _) = await fixture.CreateAccountAsync(RoleNames.Admin, OrganisationSeedConfiguration.KenyaId);
        var portal = await fixture.SignInAsync(userName);
        var fieldApp = await SignInToFieldAppAsync(userName);

        var attacker = fixture.Factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await attacker.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { UserName = userName, Password = "Wrong-passw0rd!" });
        }

        // The lockout really happened: even the right password is now refused.
        var rightPassword = await attacker.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest { UserName = userName, Password = AccessControlFixture.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, rightPassword.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fieldApp.GetAsync("/api/v1/preset-catalogues")).StatusCode);
    }

    private async Task<HttpClient> SignInToFieldAppAsync(string userName)
    {
        var client = fixture.Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = AccessControlFixture.Password,
        });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task ChangeInDatabaseAsync(Action<DotGlassesDbContext> change)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }

    private Task SetRoleInDatabaseAsync(Guid userId, string role) => ChangeInDatabaseAsync(db =>
    {
        db.UserRoles.RemoveRange(db.UserRoles.Where(ur => ur.UserId == userId));
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = db.Roles.Single(r => r.Name == role).Id });
    });

    /// <summary>Suspends through the application's own suspend operation — behind the signed-in
    /// user's back, but writing exactly the value User Directory's Suspend writes, so the test
    /// covers how that value round-trips through the database.</summary>
    private async Task SuspendInDatabaseAsync(Guid userId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUserAdminService>().SuspendAsync(userId);
    }

    private static void AssertAccessDenied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", RedirectPath(response), StringComparer.OrdinalIgnoreCase);
    }

    private static string RedirectPath(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return (location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location)).AbsolutePath;
    }

    private void SeedTest(string hierarchyPath)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        db.Tests.Add(new Test
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            TechnicianUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        db.SaveChanges();
    }

    private static int TotalTests(string dashboardHtml)
    {
        var match = Regex.Match(dashboardHtml, "Total tests</div><div class=\"dg-stat-value\">(\\d+)<");
        Assert.True(match.Success, "No Total tests figure on the Dashboard.");
        return int.Parse(match.Groups[1].Value);
    }
}

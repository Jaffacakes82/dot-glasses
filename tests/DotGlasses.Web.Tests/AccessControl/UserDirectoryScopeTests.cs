using System.Net;
using System.Net.Http.Json;
using DotGlasses.Application.Common;
using DotGlasses.Contracts.Auth;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// User Directory under the combined scope (ADR-0006): an admin *sees* a user when any one of the
/// user's org assignments is in the admin's scope, but may suspend them, reset their password or
/// change their role only when *all* of them are — otherwise a country admin could act on a DGI
/// admin who also holds a retail point in that country. Adding or removing a single assignment
/// needs only that org in scope. No assignment is special, and the last one can't be removed.
///
/// Every target account is created per test (AccessControlFixture.CreateAccountAsync).
/// </summary>
public class UserDirectoryScopeTests(AccessControlFixture fixture) : IClassFixture<AccessControlFixture>
{
    private const string LastAssignmentMessage = "suspend them instead";

    // --- Seeing a user ------------------------------------------------------------------------

    [Fact]
    public async Task AUserIsListed_WhenAnyOneOfTheirAssignmentsIsInTheCallersScope()
    {
        // The Ugandan outlet is outside Kenya; only the Kenyan assignment can list them.
        var (straddling, _) = await fixture.CreateAccountAsync(
            RoleNames.User, fixture.SecondCountryRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointId);
        var (ugandaOnly, _) = await fixture.CreateAccountAsync(RoleNames.User, fixture.SecondCountryRetailPointId);

        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        Assert.True(IsListed(await DirectoryAsync(countryAdmin, straddling), straddling));
        Assert.False(IsListed(await DirectoryAsync(countryAdmin, ugandaOnly), ugandaOnly));
    }

    [Fact]
    public async Task AUserWithNoAssignment_IsListedForADgiAdminOnly_AndCanBeAssignedAnOrgAgain()
    {
        // The application never leaves a user with no assignment; the rows go behind its back,
        // the way a database clear-down takes them.
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
            db.UserOrgAssignments.RemoveRange(db.UserOrgAssignments.Where(x => x.UserId == userId));
            await db.SaveChangesAsync();
        }

        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        Assert.False(IsListed(await DirectoryAsync(countryAdmin, userName), userName));

        var html = await DirectoryAsync(dgiAdmin, userName);
        Assert.True(IsListed(html, userName));
        Assert.Contains("No organisation", html);

        // Repaired from the portal: assigning them an org is the per-org check, as for anyone.
        var retailPoint = OrganisationSeedConfiguration.KenyaRetailPointId.ToString();
        AssertRedirectedTo("/Organisations", await PostAsync(dgiAdmin, "/Organisations/AssignUsers", ("orgNodeId", retailPoint), ("userIds", userId.ToString())));
        Assert.True(IsListed(await DirectoryAsync(countryAdmin, userName), userName));
    }

    // --- Acting on the whole user needs all of their assignments in scope ----------------------

    [Fact]
    public async Task SuspendResetPasswordAndRoleChange_AreRefused_WhenTheTargetHasAnAssignmentOutsideTheCallersScope()
    {
        // The Kenyan outlet is inside Kenya; the Uganda assignment is what puts them beyond a
        // Kenya admin.
        var (userName, userId) = await fixture.CreateAccountAsync(
            RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId, fixture.SecondCountryId);

        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        AssertAccessDenied(await PostAsync(countryAdmin, "/UserDirectory/Suspend", ("id", userId.ToString())));
        AssertAccessDenied(await PostAsync(countryAdmin, "/UserDirectory/ResetPassword", ("id", userId.ToString())));
        AssertAccessDenied(await PostAsync(countryAdmin, "/UserDirectory/ChangeRole", ("id", userId.ToString()), ("role", RoleNames.Admin)));

        // Seen (one assignment is in scope), but offered nothing it would be refused.
        var html = await DirectoryAsync(countryAdmin, userName);
        Assert.True(IsListed(html, userName));
        Assert.DoesNotContain($"value=\"{userId}\"", html);

        // The DGI admin, above both assignments, can do all three — so the refusals above are the
        // scope rule, not an unmanageable account.
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        AssertRedirectedTo("/UserDirectory", await PostAsync(dgiAdmin, "/UserDirectory/ResetPassword", ("id", userId.ToString())));
        AssertRedirectedTo("/UserDirectory", await PostAsync(dgiAdmin, "/UserDirectory/ChangeRole", ("id", userId.ToString()), ("role", RoleNames.Admin)));
        AssertRedirectedTo("/UserDirectory", await PostAsync(dgiAdmin, "/UserDirectory/Suspend", ("id", userId.ToString())));
    }

    [Fact]
    public async Task ResetPasswordAndRoleChange_AreAllowed_WhenEveryAssignmentIsInTheCallersScope()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(
            RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId, AccessControlFixture.ResellerId);
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        AssertRedirectedTo("/UserDirectory", await PostAsync(countryAdmin, "/UserDirectory/ResetPassword", ("id", userId.ToString())));
        AssertRedirectedTo("/UserDirectory", await PostAsync(countryAdmin, "/UserDirectory/ChangeRole", ("id", userId.ToString()), ("role", RoleNames.Admin)));

        // The role change really landed.
        Assert.True(IsListed(await DirectoryAsync(countryAdmin, userName, role: RoleNames.Admin), userName));
        Assert.False(IsListed(await DirectoryAsync(countryAdmin, userName, role: RoleNames.User), userName));
    }

    [Fact]
    public async Task ARoleChangeToAnUnknownRole_IsRefusedWithAMessage()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId);
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(dgiAdmin, "/UserDirectory");
        var (_, html) = await AdminPortalFactory.PostAndFollowAsync(
            dgiAdmin,
            "/UserDirectory/ChangeRole",
            AdminPortalFactory.Form(token, ("id", userId.ToString()), ("role", "Supervisor")),
            referer: "/UserDirectory");

        Assert.Contains("Choose a role.", html);
        Assert.True(IsListed(await DirectoryAsync(dgiAdmin, userName, role: RoleNames.User), userName));
    }

    // --- Adding or removing one assignment needs only that org in scope ------------------------

    [Fact]
    public async Task AddingOrRemovingOneAssignment_NeedsOnlyThatOrgInTheCallersScope()
    {
        var (_, userId) = await fixture.CreateAccountAsync(
            RoleNames.User, fixture.SecondCountryId, OrganisationSeedConfiguration.KenyaRetailPointId);
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        // In Kenya: allowed, even though the user also has an assignment outside it.
        AssertRedirectedTo("/Organisations", await PostAsync(countryAdmin, "/Organisations/AssignUsers",
            ("orgNodeId", AccessControlFixture.ResellerId.ToString()), ("userIds", userId.ToString())));
        AssertRedirectedTo("/Organisations", await PostAsync(countryAdmin, "/Organisations/UnassignUser",
            ("orgNodeId", OrganisationSeedConfiguration.KenyaRetailPointId.ToString()), ("userId", userId.ToString())));

        // In Uganda: refused.
        AssertAccessDenied(await PostAsync(countryAdmin, "/Organisations/UnassignUser",
            ("orgNodeId", fixture.SecondCountryId.ToString()), ("userId", userId.ToString())));

        Assert.Equal(
            new[] { fixture.SecondCountryId, AccessControlFixture.ResellerId }.Order(),
            (await AssignedOrgIdsAsync(userId)).Order());
    }

    [Fact]
    public async Task RemovingAUsersLastAssignment_IsRefused_WithAMessageToSuspendThemInstead()
    {
        var (_, userId) = await fixture.CreateAccountAsync(
            RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId, AccessControlFixture.ResellerId);
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        // The first assignment — once the "primary" org, which couldn't be removed — goes freely.
        var (_, first) = await UnassignAndFollowAsync(dgiAdmin, userId, OrganisationSeedConfiguration.KenyaRetailPointId);
        Assert.DoesNotContain(LastAssignmentMessage, first);

        var (_, last) = await UnassignAndFollowAsync(dgiAdmin, userId, AccessControlFixture.ResellerId);
        Assert.Contains(LastAssignmentMessage, last);

        Assert.Equal([AccessControlFixture.ResellerId], await AssignedOrgIdsAsync(userId));
    }

    [Fact]
    public async Task RemovingTheBroadestAssignment_ShrinksTheUsersScopeOnTheirNextRequest()
    {
        // Removing the DGI assignment must take DGI's scope away on the user's very next request,
        // leaving only the retail point.
        var (userName, userId) = await fixture.CreateAccountAsync(
            RoleNames.Admin, OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.KenyaRetailPointId);
        var user = await fixture.SignInAsync(userName);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/ReferenceData")).StatusCode);

        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        AssertRedirectedTo("/Organisations", await PostAsync(dgiAdmin, "/Organisations/UnassignUser",
            ("orgNodeId", OrganisationSeedConfiguration.DgiId.ToString()), ("userId", userId.ToString())));

        AssertAccessDenied(await user.GetAsync("/ReferenceData"));
    }

    // --- Invite ----------------------------------------------------------------------------------

    [Fact]
    public async Task TheInviteForm_NoLongerSaysTheFirstCheckedOrgBecomesPrimary()
    {
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var html = await dgiAdmin.GetStringAsync("/UserDirectory");

        Assert.Contains("name=\"OrgNodeIds\"", html);
        Assert.DoesNotContain("becomes the primary", html);
    }

    [Fact]
    public async Task InvitingAUserToSeveralOrgs_AssignsEveryOne_AndNoneIsSpecial()
    {
        var email = $"invitee-{Guid.NewGuid():N}@test.local";
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        AssertRedirectedTo("/UserDirectory", await PostAsync(dgiAdmin, "/UserDirectory/Invite",
            ("FullName", email),
            ("Email", email),
            ("Role", RoleNames.User),
            ("OrgNodeIds", OrganisationSeedConfiguration.KenyaRetailPointId.ToString()),
            ("OrgNodeIds", fixture.SecondCountryRetailPointId.ToString())));

        var userId = await UserIdAsync(email);
        Assert.Equal(
            new[] { OrganisationSeedConfiguration.KenyaRetailPointId, fixture.SecondCountryRetailPointId }.Order(),
            (await AssignedOrgIdsAsync(userId)).Order());

        // Whichever was ticked first, either assignment can go — until only one is left.
        var (_, first) = await UnassignAndFollowAsync(dgiAdmin, userId, OrganisationSeedConfiguration.KenyaRetailPointId);
        Assert.DoesNotContain(LastAssignmentMessage, first);
        var (_, last) = await UnassignAndFollowAsync(dgiAdmin, userId, fixture.SecondCountryRetailPointId);
        Assert.Contains(LastAssignmentMessage, last);
    }

    [Fact]
    public async Task InvitingAUserToAnOrgOutsideTheCallersScope_IsRefused()
    {
        var email = $"invitee-{Guid.NewGuid():N}@test.local";
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        var response = await PostAsync(countryAdmin, "/UserDirectory/Invite",
            ("FullName", email),
            ("Email", email),
            ("Role", RoleNames.User),
            ("OrgNodeIds", OrganisationSeedConfiguration.KenyaRetailPointId.ToString()),
            ("OrgNodeIds", fixture.SecondCountryRetailPointId.ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("outside the ones you manage", await response.Content.ReadAsStringAsync());
        Assert.Null(await FindUserIdAsync(email));
    }

    // --- Status ----------------------------------------------------------------------------------

    [Fact]
    public async Task AUserLockedOutByFailedSignIns_IsListedAsActive_WhileASuspendedOneIsListedAsSuspended()
    {
        var (lockedOut, _) = await fixture.CreateAccountAsync(RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId);
        var (suspended, suspendedId) = await fixture.CreateAccountAsync(RoleNames.User, OrganisationSeedConfiguration.KenyaRetailPointId);

        var anonymous = fixture.Factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { UserName = lockedOut, Password = "Wrong-passw0rd!" });
        }

        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        AssertRedirectedTo("/UserDirectory", await PostAsync(dgiAdmin, "/UserDirectory/Suspend", ("id", suspendedId.ToString())));

        Assert.True(IsListed(await DirectoryAsync(dgiAdmin, lockedOut, status: "Active"), lockedOut));
        Assert.False(IsListed(await DirectoryAsync(dgiAdmin, lockedOut, status: "Suspended"), lockedOut));
        Assert.True(IsListed(await DirectoryAsync(dgiAdmin, suspended, status: "Suspended"), suspended));
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static Task<string> DirectoryAsync(HttpClient client, string search, string? role = null, string? status = null) =>
        client.GetStringAsync(
            $"/UserDirectory?search={Uri.EscapeDataString(search)}&role={role}&status={status}");

    /// <summary>A listed user's name renders in its own table cell — unlike the search box, which
    /// echoes the search term whether or not anyone matched.</summary>
    private static bool IsListed(string directoryHtml, string userName) =>
        directoryHtml.Contains($"<td>{userName}</td>", StringComparison.Ordinal);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, params (string Key, string Value)[] fields) =>
        AccessControlFixture.PostFormAsync(client, path, fields);

    private static async Task<(HttpResponseMessage Redirect, string Html)> UnassignAndFollowAsync(HttpClient client, Guid userId, Guid orgNodeId)
    {
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Organisations");
        return await AdminPortalFactory.PostAndFollowAsync(
            client,
            "/Organisations/UnassignUser",
            AdminPortalFactory.Form(token, ("orgNodeId", orgNodeId.ToString()), ("userId", userId.ToString())),
            referer: "/Organisations");
    }

    private async Task<List<Guid>> AssignedOrgIdsAsync(Guid userId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        return await db.UserOrgAssignments.Where(a => a.UserId == userId).Select(a => a.OrgNodeId).ToListAsync();
    }

    private async Task<Guid> UserIdAsync(string email) =>
        await FindUserIdAsync(email) ?? throw new InvalidOperationException($"No account for {email}.");

    private async Task<Guid?> FindUserIdAsync(string email)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.FindByEmailAsync(email))?.Id;
    }

    private static void AssertAccessDenied(HttpResponseMessage response) =>
        AssertRedirectedTo("/Account/AccessDenied", response);

    private static void AssertRedirectedTo(string path, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var absolute = location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location);
        Assert.Equal(path, absolute.AbsolutePath, StringComparer.OrdinalIgnoreCase);
    }
}

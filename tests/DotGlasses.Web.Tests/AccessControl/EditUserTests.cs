using System.Net;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.AccessControl;

/// <summary>
/// The Edit user page (Spec C): role, org assignments and full name on one Save that applies only
/// what changed since the page loaded, each change under its own permission (ADR-0006); the rules
/// for editing yourself; and the organisation picker Invite and Edit share.
///
/// Every target account is created per test (AccessControlFixture.CreateAccountAsync).
/// </summary>
public class EditUserTests(AccessControlFixture fixture) : IClassFixture<AccessControlFixture>
{
    private static readonly Guid KenyaRetailPoint = OrganisationSeedConfiguration.KenyaRetailPointId;
    private static readonly Guid Kenya = OrganisationSeedConfiguration.KenyaId;

    // --- One Save -------------------------------------------------------------------------------

    [Fact]
    public async Task RoleNameAndASwapOfTheOnlyAssignment_AreSavedTogether()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var response = await PostEditAsync(admin, userId,
            fullName: "Wanjiru Kamau", loadedFullName: userName,
            role: RoleNames.Admin, loadedRole: RoleNames.User,
            orgIds: [AccessControlFixture.ResellerId], loadedOrgIds: [KenyaRetailPoint]);

        AssertRedirectedTo("/UserDirectory", response);
        var (fullName, role) = await NameAndRoleAsync(userId);
        Assert.Equal(("Wanjiru Kamau", RoleNames.Admin), (fullName, role));
        Assert.Equal([AccessControlFixture.ResellerId], await AssignedOrgIdsAsync(userId));
    }

    [Fact]
    public async Task AStaleForm_LeavesAloneAnAssignmentAddedSinceItLoaded()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        // Another admin assigns the reseller after this admin's page loaded.
        await AssignDirectlyAsync(userId, AccessControlFixture.ResellerId);

        AssertRedirectedTo("/UserDirectory", await PostEditAsync(admin, userId,
            fullName: userName, loadedFullName: userName, role: RoleNames.User, loadedRole: RoleNames.User,
            orgIds: [KenyaRetailPoint, fixture.SiblingResellerId], loadedOrgIds: [KenyaRetailPoint]));

        Assert.Equal(
            new[] { KenyaRetailPoint, AccessControlFixture.ResellerId, fixture.SiblingResellerId }.Order(),
            (await AssignedOrgIdsAsync(userId)).Order());
    }

    [Fact]
    public async Task InvitedAndSuspendedUsers_CanBeEdited_AndEditingDoesNotUnsuspend()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        AssertRedirectedTo("/UserDirectory", await PostAsync(admin, "/UserDirectory/Suspend", ("id", userId.ToString())));

        Assert.Contains("Suspended", await admin.GetStringAsync($"/UserDirectory/Edit/{userId}"));
        AssertRedirectedTo("/UserDirectory", await PostEditAsync(admin, userId,
            fullName: "Still Suspended", loadedFullName: userName, role: RoleNames.User, loadedRole: RoleNames.User,
            orgIds: [KenyaRetailPoint], loadedOrgIds: [KenyaRetailPoint]));

        using var scope = fixture.Factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString());
        Assert.Equal("Still Suspended", user!.FullName);
        Assert.True(UserSuspension.IsSuspended(user.LockoutEnd));
    }

    [Fact]
    public async Task TheDirectory_OffersEditOnEveryRow_AndHeadsTheColumnOrganisations()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var html = await admin.GetStringAsync($"/UserDirectory?search={Uri.EscapeDataString(userName)}");

        Assert.Contains($"/UserDirectory/Edit/{userId}", html);
        Assert.Contains("<th>Organisations</th>", html);
        Assert.DoesNotContain("<th>Scope</th>", html);
        Assert.DoesNotContain("No email sending is wired up", html);
    }

    // --- A user partly outside the caller's scope -------------------------------------------------

    [Fact]
    public async Task AUserPartlyOutOfScope_ShowsAReadOnlyRoleAndACountOfHiddenOrganisations()
    {
        var (_, userId) = await fixture.CreateAccountAsync(RoleNames.User, fixture.SecondCountryId, KenyaRetailPoint);
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        var html = await countryAdmin.GetStringAsync($"/UserDirectory/Edit/{userId}");

        Assert.Contains("plus 1 organisation outside your scope", html);
        Assert.DoesNotContain("Uganda", html);
        Assert.DoesNotContain("<select class=\"form-select\" id=\"Role\"", html);
        Assert.Contains("also has organisations outside your scope", html);
    }

    [Fact]
    public async Task AUserPartlyOutOfScope_CannotHaveTheirRoleOrNameChanged()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, fixture.SecondCountryId, KenyaRetailPoint);
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        AssertAccessDenied(await PostEditAsync(countryAdmin, userId,
            fullName: userName, loadedFullName: userName, role: RoleNames.Admin, loadedRole: RoleNames.User,
            orgIds: [KenyaRetailPoint], loadedOrgIds: [KenyaRetailPoint]));
        AssertAccessDenied(await PostEditAsync(countryAdmin, userId,
            fullName: "Renamed", loadedFullName: userName, role: RoleNames.User, loadedRole: RoleNames.User,
            orgIds: [KenyaRetailPoint], loadedOrgIds: [KenyaRetailPoint]));

        Assert.Equal((userName, RoleNames.User), await NameAndRoleAsync(userId));
    }

    [Fact]
    public async Task AnOrganisationOutsideTheCallersScope_CannotBeAddedOrRemoved()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, fixture.SecondCountryId, KenyaRetailPoint);
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        // A tampered form naming the hidden Ugandan assignment as "loaded, now unticked".
        AssertAccessDenied(await PostEditAsync(countryAdmin, userId,
            fullName: userName, loadedFullName: userName, role: RoleNames.User, loadedRole: RoleNames.User,
            orgIds: [KenyaRetailPoint], loadedOrgIds: [KenyaRetailPoint, fixture.SecondCountryId]));
        AssertAccessDenied(await PostEditAsync(countryAdmin, userId,
            fullName: userName, loadedFullName: userName, role: RoleNames.User, loadedRole: RoleNames.User,
            orgIds: [KenyaRetailPoint, fixture.SecondCountryRetailPointId], loadedOrgIds: [KenyaRetailPoint]));

        Assert.Equal(new[] { fixture.SecondCountryId, KenyaRetailPoint }.Order(), (await AssignedOrgIdsAsync(userId)).Order());
    }

    [Fact]
    public async Task RemovingEveryVisibleAssignment_Succeeds_WhenOneRemainsElsewhere_AndSaysTheyLeftTheScope()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, fixture.SecondCountryId, KenyaRetailPoint);
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        AssertRedirectedTo("/UserDirectory", await PostEditAsync(countryAdmin, userId,
            fullName: userName, loadedFullName: userName, role: RoleNames.User, loadedRole: RoleNames.User,
            orgIds: [], loadedOrgIds: [KenyaRetailPoint]));

        Assert.Contains("is no longer in your scope", await countryAdmin.GetStringAsync("/UserDirectory"));
        Assert.Equal([fixture.SecondCountryId], await AssignedOrgIdsAsync(userId));
    }

    [Fact]
    public async Task RemovingEveryAssignment_IsRefused_WhenNoneRemainsAnywhere_AndReturnsToTheEditPage()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(admin, $"/UserDirectory/Edit/{userId}");

        var (redirect, html) = await AdminPortalFactory.PostAndFollowAsync(
            admin, $"/UserDirectory/Edit/{userId}",
            AdminPortalFactory.Form(token, EditFields(userName, userName, RoleNames.User, RoleNames.User, [], [KenyaRetailPoint])),
            referer: $"/UserDirectory/Edit/{userId}");

        Assert.Contains($"/UserDirectory/Edit/{userId}", redirect.Headers.Location?.ToString());
        Assert.Contains("suspend them instead", html);
        Assert.Equal([KenyaRetailPoint], await AssignedOrgIdsAsync(userId));
    }

    // --- Editing yourself -------------------------------------------------------------------------

    [Fact]
    public async Task YourOwnRole_IsReadOnly_AndAChangeIsRefused()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.Admin, Kenya);
        var self = await fixture.SignInAsync(userName);

        var html = await self.GetStringAsync($"/UserDirectory/Edit/{userId}");
        Assert.Contains("Another admin must change it", html);
        Assert.DoesNotContain("<select class=\"form-select\" id=\"Role\"", html);

        var (_, afterEdit) = await PostEditAndFollowAsync(self, userId,
            EditFields(userName, userName, RoleNames.User, RoleNames.Admin, [Kenya], [Kenya]));
        Assert.Contains("You can&#x27;t change your own role", afterEdit);

        var (_, afterChangeRole) = await PostAndFollowAsync(self, "/UserDirectory/ChangeRole", "/UserDirectory",
            ("id", userId.ToString()), ("role", RoleNames.User));
        Assert.Contains("You can&#x27;t change your own role", afterChangeRole);

        Assert.Equal(RoleNames.Admin, (await NameAndRoleAsync(userId)).Role);
    }

    [Fact]
    public async Task SuspendingYourself_IsRefused()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.Admin, Kenya);
        var self = await fixture.SignInAsync(userName);

        var (_, html) = await PostAndFollowAsync(self, "/UserDirectory/Suspend", "/UserDirectory", ("id", userId.ToString()));

        Assert.Contains("You can&#x27;t suspend yourself", html);
        using var scope = fixture.Factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString());
        Assert.False(UserSuspension.IsSuspended(user!.LockoutEnd));
    }

    [Fact]
    public async Task YourOwnName_CanBeChanged_AndYouCanAssignYourselfWithinYourScope()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.Admin, Kenya);
        var self = await fixture.SignInAsync(userName);

        AssertRedirectedTo("/UserDirectory", await PostEditAsync(self, userId,
            fullName: "My Own Name", loadedFullName: userName, role: RoleNames.Admin, loadedRole: RoleNames.Admin,
            orgIds: [Kenya, KenyaRetailPoint], loadedOrgIds: [Kenya]));

        Assert.Equal("My Own Name", (await NameAndRoleAsync(userId)).FullName);
        Assert.Equal(new[] { Kenya, KenyaRetailPoint }.Order(), (await AssignedOrgIdsAsync(userId)).Order());
    }

    [Fact]
    public async Task RemovingYourOwnAssignment_IsAllowedOnlyWhenYourScopeDoesNotShrink_OnTheEditPage()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.Admin, Kenya, KenyaRetailPoint);
        var self = await fixture.SignInAsync(userName);

        // The outer assignment: removing it would leave only the retail point.
        var (_, refused) = await PostEditAndFollowAsync(self, userId,
            EditFields(userName, userName, RoleNames.Admin, RoleNames.Admin, [KenyaRetailPoint], [Kenya, KenyaRetailPoint]));
        Assert.Contains("Ask another admin to remove it", refused);
        Assert.Equal(new[] { Kenya, KenyaRetailPoint }.Order(), (await AssignedOrgIdsAsync(userId)).Order());

        // The nested one: Kenya still covers it.
        AssertRedirectedTo("/UserDirectory", await PostEditAsync(self, userId,
            fullName: userName, loadedFullName: userName, role: RoleNames.Admin, loadedRole: RoleNames.Admin,
            orgIds: [Kenya], loadedOrgIds: [Kenya, KenyaRetailPoint]));
        Assert.Equal([Kenya], await AssignedOrgIdsAsync(userId));
    }

    [Fact]
    public async Task AssigningAndUnassigningYourself_FollowTheSameRules_OnTheOrganisationsScreen()
    {
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.Admin, Kenya);
        var self = await fixture.SignInAsync(userName);

        AssertRedirectedTo("/Organisations", await PostAsync(self, "/Organisations/AssignUsers",
            ("orgNodeId", KenyaRetailPoint.ToString()), ("userIds", userId.ToString())));
        Assert.Equal(new[] { Kenya, KenyaRetailPoint }.Order(), (await AssignedOrgIdsAsync(userId)).Order());

        var (_, refused) = await PostAndFollowAsync(self, "/Organisations/UnassignUser", "/Organisations",
            ("orgNodeId", Kenya.ToString()), ("userId", userId.ToString()));
        Assert.Contains("Ask another admin to remove it", refused);

        AssertRedirectedTo("/Organisations", await PostAsync(self, "/Organisations/UnassignUser",
            ("orgNodeId", KenyaRetailPoint.ToString()), ("userId", userId.ToString())));
        Assert.Equal([Kenya], await AssignedOrgIdsAsync(userId));
    }

    // --- The organisation picker ------------------------------------------------------------------

    [Fact]
    public async Task ADeactivatedOrganisation_IsNotOfferedOnInvite_ButAnExistingAssignmentToOneIsListedOnEdit()
    {
        var closedOutlet = await AddOrganisationAsync($"Closed Outlet {Guid.NewGuid():N}", OrganisationLevel.RetailPoint, AccessControlFixture.ResellerId, deactivated: true);
        var (_, userId) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint, closedOutlet.Id);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var directory = await admin.GetStringAsync("/UserDirectory");
        Assert.DoesNotContain($"value=\"{closedOutlet.Id}\"", directory);
        // The picker shows each organisation's level as words.
        Assert.Contains("· Retail Point", directory);
        Assert.Contains("· Retailer/distributor", directory);
        Assert.Contains("Only a direct assignment to a Retail Point", directory);

        var edit = await admin.GetStringAsync($"/UserDirectory/Edit/{userId}");
        Assert.Contains($"value=\"{closedOutlet.Id}\" checked", edit);
        Assert.Contains(">deactivated</span>", edit);

        // ...and it can be unticked.
        AssertRedirectedTo("/UserDirectory", await PostEditAsync(admin, userId,
            fullName: null, loadedFullName: null, role: RoleNames.User, loadedRole: RoleNames.User,
            orgIds: [KenyaRetailPoint], loadedOrgIds: [KenyaRetailPoint, closedOutlet.Id]));
        Assert.Equal([KenyaRetailPoint], await AssignedOrgIdsAsync(userId));
    }

    [Fact]
    public async Task NobodyCanBeAssignedToADeactivatedOrganisation()
    {
        var closedOutlet = await AddOrganisationAsync($"Closed Outlet {Guid.NewGuid():N}", OrganisationLevel.RetailPoint, AccessControlFixture.ResellerId, deactivated: true);
        var (userName, userId) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var (_, html) = await PostEditAndFollowAsync(admin, userId,
            EditFields(userName, userName, RoleNames.User, RoleNames.User, [KenyaRetailPoint, closedOutlet.Id], [KenyaRetailPoint]));

        Assert.Contains("is deactivated, so nobody can be assigned to it", html);
        Assert.Equal([KenyaRetailPoint], await AssignedOrgIdsAsync(userId));
    }

    // --- Helpers ----------------------------------------------------------------------------------

    private static (string Key, string Value)[] EditFields(
        string? fullName, string? loadedFullName, string role, string loadedRole, Guid[] orgIds, Guid[] loadedOrgIds) =>
        [
            ("FullName", fullName ?? string.Empty),
            ("LoadedFullName", loadedFullName ?? string.Empty),
            ("Role", role),
            ("LoadedRole", loadedRole),
            .. orgIds.Select(id => ("OrgNodeIds", id.ToString())),
            .. loadedOrgIds.Select(id => ("LoadedOrgNodeIds", id.ToString())),
        ];

    private static Task<HttpResponseMessage> PostEditAsync(
        HttpClient client, Guid userId, string? fullName, string? loadedFullName, string role, string loadedRole, Guid[] orgIds, Guid[] loadedOrgIds) =>
        PostAsync(client, $"/UserDirectory/Edit/{userId}", EditFields(fullName, loadedFullName, role, loadedRole, orgIds, loadedOrgIds));

    private static Task<(HttpResponseMessage Redirect, string Html)> PostEditAndFollowAsync(
        HttpClient client, Guid userId, (string Key, string Value)[] fields) =>
        PostAndFollowAsync(client, $"/UserDirectory/Edit/{userId}", $"/UserDirectory/Edit/{userId}", fields);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, params (string Key, string Value)[] fields) =>
        AccessControlFixture.PostFormAsync(client, path, fields);

    private static async Task<(HttpResponseMessage Redirect, string Html)> PostAndFollowAsync(
        HttpClient client, string path, string referer, params (string Key, string Value)[] fields)
    {
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Organisations");
        return await AdminPortalFactory.PostAndFollowAsync(client, path, AdminPortalFactory.Form(token, fields), referer);
    }

    private async Task<OrganisationNode> AddOrganisationAsync(string name, OrganisationLevel level, Guid parentId, bool deactivated = false)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var parent = await db.OrganisationNodes.IgnoreQueryFilters().FirstAsync(o => o.Id == parentId);
        var node = new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Name = name,
            Level = level,
            HierarchyPath = $"{parent.HierarchyPath}{Random.Shared.Next(100_000, int.MaxValue)}/",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsDeleted = deactivated,
            DeletedAtUtc = deactivated ? DateTimeOffset.UtcNow : null,
        };
        db.OrganisationNodes.Add(node);
        await db.SaveChangesAsync();
        return node;
    }

    private async Task AssignDirectlyAsync(Guid userId, Guid orgNodeId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        db.UserOrgAssignments.Add(new UserOrgAssignment { Id = Guid.NewGuid(), UserId = userId, OrgNodeId = orgNodeId, CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    private async Task<List<Guid>> AssignedOrgIdsAsync(Guid userId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        return await db.UserOrgAssignments.Where(a => a.UserId == userId).Select(a => a.OrgNodeId).ToListAsync();
    }

    private async Task<(string? FullName, string? Role)> NameAndRoleAsync(Guid userId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        return (user!.FullName, RoleNames.Primary(await users.GetRolesAsync(user)));
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

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
/// The Organisations screen after Spec C: no Kind, levels shown as words, several users assigned
/// in one go, and deactivating an organisation with everything beneath it — reactivated as the
/// same group, never under a deactivated parent — while the reports keep its name.
///
/// Organisations that get deactivated are created per test; the seeded tree is never touched.
/// </summary>
public class OrganisationsManagementTests(AccessControlFixture fixture) : IClassFixture<AccessControlFixture>
{
    private static readonly Guid Kenya = OrganisationSeedConfiguration.KenyaId;
    private static readonly Guid KenyaRetailPoint = OrganisationSeedConfiguration.KenyaRetailPointId;

    // --- Kind is gone; levels read as words -------------------------------------------------------

    [Fact]
    public async Task ThePageAndTheCsv_CarryNoKind_AndShowLevelsAsWords()
    {
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var html = await admin.GetStringAsync($"/Organisations?selectedId={Kenya}");
        Assert.DoesNotContain("name=\"Kind\"", html);
        Assert.DoesNotContain("Kind (optional", html);
        Assert.Contains(">Retailer/distributor</span>", html);
        Assert.Contains(">Retail Point</span>", html);
        Assert.Contains(">DGI</span>", html);
        Assert.DoesNotContain(">Intermediate<", html);
        Assert.DoesNotContain(">RetailPoint<", html);

        // Kenya can hold a Retailer/distributor or a Retail Point; the dropdown posts the level
        // itself and shows the words.
        Assert.Contains("<option value=\"Intermediate\">Retailer/distributor</option>", html);
        Assert.Contains("<option value=\"RetailPoint\">Retail Point</option>", html);

        var csv = await admin.GetStringAsync("/Organisations/Export");
        var header = csv.Split('\n')[0];
        Assert.DoesNotContain("Kind", header);
        Assert.Contains("Retailer/distributor", csv);
        Assert.Contains("Retail Point", csv);
        Assert.DoesNotContain("RetailPoint", csv);
    }

    [Fact]
    public async Task ARetailPoint_OffersNoAddAction_WhateverItsLabelSays()
    {
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var html = await admin.GetStringAsync($"/Organisations?selectedId={KenyaRetailPoint}");

        Assert.DoesNotContain("id=\"addChildModal\"", html);
        Assert.Contains("Everyone you tick will be able to record here in the Field App.", html);
    }

    // --- Assigning several users at once ----------------------------------------------------------

    [Fact]
    public async Task SeveralUsers_AreAssignedInOneRequest()
    {
        var (_, first) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var (_, second) = await fixture.CreateAccountAsync(RoleNames.Admin, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        AssertRedirectedTo("/Organisations", await PostAsync(admin, "/Organisations/AssignUsers",
            ("orgNodeId", fixture.SiblingResellerId.ToString()), ("userIds", first.ToString()), ("userIds", second.ToString())));

        Assert.Contains(fixture.SiblingResellerId, await AssignedOrgIdsAsync(first));
        Assert.Contains(fixture.SiblingResellerId, await AssignedOrgIdsAsync(second));
    }

    [Fact]
    public async Task TheDialog_OffersOnlyActiveUsersNotAlreadyAssigned_WithTheirRoles()
    {
        var (active, activeId) = await fixture.CreateAccountAsync(RoleNames.Admin, KenyaRetailPoint);
        var (_, suspendedId) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var (_, alreadyId) = await fixture.CreateAccountAsync(RoleNames.User, fixture.SiblingResellerId);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        AssertRedirectedTo("/UserDirectory", await PostAsync(admin, "/UserDirectory/Suspend", ("id", suspendedId.ToString())));

        var html = await admin.GetStringAsync($"/Organisations?selectedId={fixture.SiblingResellerId}");

        Assert.Contains($"name=\"userIds\" value=\"{activeId}\"", html);
        Assert.Contains($"{active} <span class=\"dg-muted\">· Admin", html);
        Assert.DoesNotContain($"name=\"userIds\" value=\"{suspendedId}\"", html);
        Assert.DoesNotContain($"name=\"userIds\" value=\"{alreadyId}\"", html);
        Assert.Contains("gets Admin Portal access to", html);
    }

    [Theory]
    [InlineData("suspended")]
    [InlineData("invited")]
    [InlineData("unseen")]
    public async Task OneUserWhoCannotBeAssigned_LeavesNobodyAssigned(string kind)
    {
        var (_, good) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(kind == "unseen" ? AccessControlFixture.CountryAdmin : AccessControlFixture.DgiAdmin);

        Guid bad;
        string expected;
        switch (kind)
        {
            case "suspended":
                (_, bad) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint);
                AssertRedirectedTo("/UserDirectory", await PostAsync(admin, "/UserDirectory/Suspend", ("id", bad.ToString())));
                expected = "is suspended, so nobody was assigned";
                break;
            case "invited":
                bad = await CreateInvitedAsync(KenyaRetailPoint);
                expected = "is invited, so nobody was assigned";
                break;
            default:
                // Uganda only: a Kenya admin can't see them, so can't assign them.
                (_, bad) = await fixture.CreateAccountAsync(RoleNames.User, fixture.SecondCountryId);
                expected = "isn&#x27;t in the organisations you manage, so nobody was assigned";
                break;
        }

        var (_, html) = await PostAndFollowAsync(admin, "/Organisations/AssignUsers", "/Organisations",
            ("orgNodeId", fixture.SiblingResellerId.ToString()), ("userIds", good.ToString()), ("userIds", bad.ToString()));

        Assert.Contains(expected, html);
        Assert.DoesNotContain(fixture.SiblingResellerId, await AssignedOrgIdsAsync(good));
        Assert.DoesNotContain(fixture.SiblingResellerId, await AssignedOrgIdsAsync(bad));
    }

    [Fact]
    public async Task UnassigningAtARetailPoint_AsksFirst_WithTheEditPagesCopy()
    {
        var (userName, _) = await fixture.CreateAccountAsync(RoleNames.User, KenyaRetailPoint, Kenya);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        var atRetailPoint = await admin.GetStringAsync($"/Organisations?selectedId={KenyaRetailPoint}");
        Assert.Contains($"data-dg-confirm=\"Remove {userName} from", atRetailPoint);
        Assert.Contains("will be refused", atRetailPoint);

        var higherUp = await admin.GetStringAsync($"/Organisations?selectedId={Kenya}");
        Assert.DoesNotContain($"data-dg-confirm=\"Remove {userName} from", higherUp);
    }

    // --- Deactivating and reactivating a whole group ------------------------------------------------

    [Fact]
    public async Task DeactivatingAnOrganisation_TakesEverythingBeneathIt_AndReactivatingRestoresExactlyThatGroup()
    {
        var retailer = await AddOrganisationAsync($"Closing Retailer {Guid.NewGuid():N}", OrganisationLevel.Intermediate, Kenya);
        var outlet = await AddOrganisationAsync($"Closing Outlet {Guid.NewGuid():N}", OrganisationLevel.RetailPoint, retailer.Id);
        var earlier = await AddOrganisationAsync($"Closed Earlier {Guid.NewGuid():N}", OrganisationLevel.RetailPoint, retailer.Id);
        var (_, technician) = await fixture.CreateAccountAsync(RoleNames.User, outlet.Id);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        // One outlet was closed on its own first.
        AssertRedirectedTo("/Organisations", await PostAsync(admin, "/Organisations/SetActive", ("id", earlier.Id.ToString()), ("value", "false")));

        // The confirmation states what goes with the retailer.
        var before = await admin.GetStringAsync($"/Organisations?selectedId={retailer.Id}");
        Assert.Contains("<strong>1</strong> organisation beneath it is deactivated with it.", before);
        Assert.Contains("<strong>1</strong> person loses", before);
        Assert.Contains("Unsent Field App records for its retail points will be refused", before);

        AssertRedirectedTo("/Organisations", await PostAsync(admin, "/Organisations/SetActive", ("id", retailer.Id.ToString()), ("value", "false")));

        Assert.All(await ActiveFlagsAsync(retailer.Id, outlet.Id, earlier.Id), active => Assert.False(active));

        // The strip lists the group once, by its top organisation; the outlets beneath it aren't
        // offered on their own while it is deactivated.
        var strip = await admin.GetStringAsync("/Organisations");
        Assert.Contains($"{retailer.Name} and 1 beneath it", strip);
        Assert.DoesNotContain($"name=\"id\" value=\"{outlet.Id}\"", strip);
        Assert.DoesNotContain($"name=\"id\" value=\"{earlier.Id}\"", strip);

        // Nothing is re-parented, and assignments inside the group are untouched.
        Assert.Equal(retailer.Id, await ParentIdAsync(outlet.Id));
        Assert.Equal([outlet.Id], await AssignedOrgIdsAsync(technician));

        // A child can't come back under a deactivated parent.
        var (_, refused) = await PostAndFollowAsync(admin, "/Organisations/SetActive", "/Organisations", ("id", outlet.Id.ToString()), ("value", "true"));
        Assert.Contains("Reactivate the organisation above it first", refused);
        Assert.False((await ActiveFlagsAsync(outlet.Id)).Single());

        // Reactivating the retailer restores what went with it, and leaves the earlier closure.
        AssertRedirectedTo("/Organisations", await PostAsync(admin, "/Organisations/SetActive", ("id", retailer.Id.ToString()), ("value", "true")));
        Assert.Equal([true, true, false], await ActiveFlagsAsync(retailer.Id, outlet.Id, earlier.Id));

        // The page is sound afterwards: the retailer is back in the tree, and the earlier closure
        // is now offered on its own.
        var after = await admin.GetStringAsync($"/Organisations?selectedId={retailer.Id}");
        Assert.Contains(outlet.Name, after);
        Assert.Contains($"name=\"id\" value=\"{earlier.Id}\"", after);
        Assert.DoesNotContain($"{retailer.Name} and 1 beneath it", after);

        AssertRedirectedTo("/Organisations", await PostAsync(admin, "/Organisations/SetActive", ("id", earlier.Id.ToString()), ("value", "true")));
        Assert.True((await ActiveFlagsAsync(earlier.Id)).Single());
    }

    [Fact]
    public async Task AnOrganisationYourOwnAccessComesThrough_CannotBeDeactivated()
    {
        var countryAdmin = await fixture.SignInAsync(AccessControlFixture.CountryAdmin);

        // The button isn't offered on their own country...
        var html = await countryAdmin.GetStringAsync($"/Organisations?selectedId={Kenya}");
        Assert.Contains("Your own access comes through this organisation", html);
        Assert.DoesNotContain("id=\"deactivateModal\"", html);

        // ...and a posted request is refused: Kenya stays active.
        var (_, refused) = await PostAndFollowAsync(countryAdmin, "/Organisations/SetActive", "/Organisations", ("id", Kenya.ToString()), ("value", "false"));
        Assert.Contains("deactivating it would lock you out", refused);
        Assert.True((await ActiveFlagsAsync(Kenya)).Single());

        // Nobody sits above the root, so it can't be deactivated at all.
        var dgiAdmin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);
        var (_, rootRefused) = await PostAndFollowAsync(dgiAdmin, "/Organisations/SetActive", "/Organisations",
            ("id", OrganisationSeedConfiguration.DgiId.ToString()), ("value", "false"));
        Assert.Contains("deactivating it would lock you out", rootRefused);
        Assert.True((await ActiveFlagsAsync(OrganisationSeedConfiguration.DgiId)).Single());
    }

    // --- Reports keep a deactivated organisation's name -------------------------------------------

    [Fact]
    public async Task RecordsAtADeactivatedRetailPoint_KeepCounting_UnderItsNameMarkedDeactivated()
    {
        var retailer = await AddOrganisationAsync($"Report Retailer {Guid.NewGuid():N}", OrganisationLevel.Intermediate, Kenya);
        var closedOutlet = await AddOrganisationAsync($"Report Outlet {Guid.NewGuid():N}", OrganisationLevel.RetailPoint, retailer.Id);
        var closedRetailer = await AddOrganisationAsync($"Closed Retailer {Guid.NewGuid():N}", OrganisationLevel.Intermediate, Kenya);
        var outletUnderClosedRetailer = await AddOrganisationAsync($"Orphan Outlet {Guid.NewGuid():N}", OrganisationLevel.RetailPoint, closedRetailer.Id);
        await AddSaleAsync(closedOutlet.HierarchyPath);
        await AddSaleAsync(outletUnderClosedRetailer.HierarchyPath);
        var (userName, _) = await fixture.CreateAccountAsync(RoleNames.User, closedOutlet.Id, KenyaRetailPoint);
        var admin = await fixture.SignInAsync(AccessControlFixture.DgiAdmin);

        AssertRedirectedTo("/Organisations", await PostAsync(admin, "/Organisations/SetActive", ("id", closedOutlet.Id.ToString()), ("value", "false")));
        AssertRedirectedTo("/Organisations", await PostAsync(admin, "/Organisations/SetActive", ("id", closedRetailer.Id.ToString()), ("value", "false")));

        var eventHistory = await admin.GetStringAsync("/EventHistory?tab=sales");
        Assert.Contains($"{closedOutlet.Name} (deactivated)", eventHistory);
        Assert.Contains($"{outletUnderClosedRetailer.Name} (deactivated)", eventHistory);

        var dashboard = await admin.GetStringAsync("/");
        Assert.Contains($"{closedOutlet.Name} (deactivated)", dashboard);
        Assert.Contains($"{closedRetailer.Name} (deactivated)", dashboard);

        // The User Directory marks the assignment too.
        var directory = await admin.GetStringAsync($"/UserDirectory?search={Uri.EscapeDataString(userName)}");
        Assert.Contains($"{closedOutlet.Name} (deactivated)", directory);
    }

    // --- Helpers ----------------------------------------------------------------------------------

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, params (string Key, string Value)[] fields) =>
        AccessControlFixture.PostFormAsync(client, path, fields);

    private static async Task<(HttpResponseMessage Redirect, string Html)> PostAndFollowAsync(
        HttpClient client, string path, string referer, params (string Key, string Value)[] fields)
    {
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Organisations");
        return await AdminPortalFactory.PostAndFollowAsync(client, path, AdminPortalFactory.Form(token, fields), referer);
    }

    private async Task<OrganisationNode> AddOrganisationAsync(string name, OrganisationLevel level, Guid parentId)
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
        };
        db.OrganisationNodes.Add(node);
        await db.SaveChangesAsync();
        return node;
    }

    private async Task AddSaleAsync(string hierarchyPath)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var customer = new Customer { Id = Guid.NewGuid(), FullName = "Report Customer", PhoneNumber = "0700000000", HierarchyPath = hierarchyPath };
        db.Customers.Add(customer);
        db.Sales.Add(new Sale
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            TechnicianUserId = Guid.NewGuid(),
            CustomerId = customer.Id,
            ConsentGiven = true,
            LensRangeType = LensRangeType.Custom,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateInvitedAsync(Guid orgNodeId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"invited-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = email };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, RoleNames.User)).Succeeded);
        db.UserOrgAssignments.Add(new UserOrgAssignment { Id = Guid.NewGuid(), UserId = user.Id, OrgNodeId = orgNodeId, CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<List<Guid>> AssignedOrgIdsAsync(Guid userId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        return await db.UserOrgAssignments.Where(a => a.UserId == userId).Select(a => a.OrgNodeId).ToListAsync();
    }

    private async Task<Guid?> ParentIdAsync(Guid orgId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        return (await db.OrganisationNodes.IgnoreQueryFilters().SingleAsync(o => o.Id == orgId)).ParentId;
    }

    private async Task<List<bool>> ActiveFlagsAsync(params Guid[] orgIds)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var deleted = await db.OrganisationNodes.IgnoreQueryFilters()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.IsDeleted);
        return orgIds.Select(id => !deleted[id]).ToList();
    }

    private static void AssertRedirectedTo(string path, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var absolute = location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location);
        Assert.Equal(path, absolute.AbsolutePath, StringComparer.OrdinalIgnoreCase);
    }
}

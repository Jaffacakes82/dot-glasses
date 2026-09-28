using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotGlasses.Application.Common;
using DotGlasses.Contracts.Auth;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests;

/// <summary>
/// The Field App works at one current location (ADR-0006): an active retail point the user is
/// directly assigned to. Sign-in chooses it, switching reissues the token for another, and every
/// Field App request is scoped to it alone — re-validated on each request, so a location the user
/// has lost since signing in stops showing anything on the very next request.
///
/// Every client here signs in through the real /api/v1/auth/login, so the token under test is the
/// one the Field App would hold. Each test builds its own retail points and account, so none sees a
/// neighbour's rows through the shared database.
/// </summary>
[Collection(WebApiCollection.Name)]
public class FieldAppCurrentLocationApiTests(CustomWebApplicationFactory factory)
{
    private const string Password = "TestPassw0rd!";

    // --- "My orgs" -------------------------------------------------------------------------------

    [Fact]
    public async Task MyOrgs_ListsOnlyTheActiveRetailPointsTheUserIsDirectlyAssignedTo()
    {
        var retailer = NewOrg(OrganisationLevel.Intermediate, OrganisationSeedConfiguration.KenyaPath);
        var outlet = NewOrg(OrganisationLevel.RetailPoint);
        var deactivatedOutlet = NewOrg(OrganisationLevel.RetailPoint, deactivated: true);
        NewOrg(OrganisationLevel.RetailPoint, retailer.Path); // beneath an assignment, not assigned itself
        var userName = await CreateAccountAsync(outlet.Id, deactivatedOutlet.Id, retailer.Id, OrganisationSeedConfiguration.KenyaId);

        var (client, _) = await SignInAsync(userName);
        var orgs = await client.GetFromJsonAsync<List<AssignedOrgDto>>("api/v1/auth/my-orgs");

        Assert.Equal([outlet.Id], orgs!.Select(o => o.OrgNodeId));
    }

    // --- Sign-in ---------------------------------------------------------------------------------

    [Fact]
    public async Task SignIn_WithARememberedLocationThatIsStillEligible_IssuesATokenForIt()
    {
        var first = NewOrg(OrganisationLevel.RetailPoint);
        var second = NewOrg(OrganisationLevel.RetailPoint);
        var userName = await CreateAccountAsync(first.Id, second.Id);

        var (client, login) = await SignInAsync(userName, preferredLocationId: second.Id);

        Assert.Equal(second.Id, login.CurrentLocationId);
        Assert.Equal(second.Name, login.CurrentLocationName);
        var orgs = await client.GetFromJsonAsync<List<AssignedOrgDto>>("api/v1/auth/my-orgs");
        Assert.Equal(second.Id, Assert.Single(orgs!, o => o.IsActive).OrgNodeId);
    }

    [Fact]
    public async Task SignIn_WithARememberedLocationNoLongerEligible_AndSeveralEligible_IssuesATokenWithNoLocation()
    {
        var first = NewOrg(OrganisationLevel.RetailPoint);
        var second = NewOrg(OrganisationLevel.RetailPoint);
        var lost = NewOrg(OrganisationLevel.RetailPoint);
        var userName = await CreateAccountAsync(first.Id, second.Id);
        SeedLead(first.Path);

        var (client, login) = await SignInAsync(userName, preferredLocationId: lost.Id);

        Assert.Null(login.CurrentLocationId);
        Assert.DoesNotContain(await client.GetFromJsonAsync<List<AssignedOrgDto>>("api/v1/auth/my-orgs") ?? [], o => o.IsActive);
        Assert.Empty((await client.GetFromJsonAsync<List<LeadDto>>("api/v1/leads"))!);
    }

    [Fact]
    public async Task SignIn_WhenExactlyOneLocationIsEligible_IssuesATokenForIt_WhateverWasRemembered()
    {
        var outlet = NewOrg(OrganisationLevel.RetailPoint);
        var lost = NewOrg(OrganisationLevel.RetailPoint);
        var userName = await CreateAccountAsync(outlet.Id, OrganisationSeedConfiguration.KenyaId);

        var (_, remembered) = await SignInAsync(userName, preferredLocationId: lost.Id);
        var (_, fresh) = await SignInAsync(userName);

        Assert.Equal(outlet.Id, remembered.CurrentLocationId);
        Assert.Equal(outlet.Id, fresh.CurrentLocationId);
    }

    // --- Switching -------------------------------------------------------------------------------

    [Fact]
    public async Task SwitchLocation_IssuesATokenForTheChosenLocation_AndWritesNothingToTheUserRow()
    {
        var first = NewOrg(OrganisationLevel.RetailPoint);
        var second = NewOrg(OrganisationLevel.RetailPoint);
        var userName = await CreateAccountAsync(first.Id, second.Id);
        var (client, _) = await SignInAsync(userName, preferredLocationId: first.Id);
        var stampBefore = await ConcurrencyStampAsync(userName);

        var response = await client.PostAsJsonAsync("api/v1/auth/switch-org", new SwitchOrgRequest { OrgNodeId = second.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var switched = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(second.Id, switched.CurrentLocationId);
        var orgs = await WithToken(switched.AccessToken).GetFromJsonAsync<List<AssignedOrgDto>>("api/v1/auth/my-orgs");
        Assert.Equal(second.Id, Assert.Single(orgs!, o => o.IsActive).OrgNodeId);

        // Identity rotates the concurrency stamp on every write to the user row.
        Assert.Equal(stampBefore, await ConcurrencyStampAsync(userName));
    }

    private async Task<string?> ConcurrencyStampAsync(string userName)
    {
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(userName);
        return user!.ConcurrencyStamp;
    }

    [Fact]
    public async Task SwitchLocation_ToADeactivatedRetailPoint_IsRefused()
    {
        var outlet = NewOrg(OrganisationLevel.RetailPoint);
        var deactivated = NewOrg(OrganisationLevel.RetailPoint, deactivated: true);
        var userName = await CreateAccountAsync(outlet.Id, deactivated.Id);
        var (client, _) = await SignInAsync(userName);

        var response = await client.PostAsJsonAsync("api/v1/auth/switch-org", new SwitchOrgRequest { OrgNodeId = deactivated.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Scoped to the current location ---------------------------------------------------------

    [Fact]
    public async Task TheLeadsList_AndTheConversionMatch_AreScopedToTheCurrentLocation()
    {
        var here = NewOrg(OrganisationLevel.RetailPoint);
        var elsewhere = NewOrg(OrganisationLevel.RetailPoint);
        var userName = await CreateAccountAsync(here.Id, elsewhere.Id);
        var (leadHere, customerHere) = SeedLead(here.Path);
        var (leadElsewhere, customerElsewhere) = SeedLead(elsewhere.Path);

        var (client, _) = await SignInAsync(userName, preferredLocationId: here.Id);

        var leads = (await client.GetFromJsonAsync<List<LeadDto>>("api/v1/leads"))!.Select(l => l.Id).ToList();
        Assert.Contains(leadHere, leads);
        Assert.DoesNotContain(leadElsewhere, leads);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MatchUrl(customerHere))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync(MatchUrl(customerElsewhere))).StatusCode);
    }

    [Fact]
    public async Task LensSetAvailability_IsScopedToTheCurrentLocation()
    {
        var here = NewOrg(OrganisationLevel.RetailPoint);
        var elsewhere = NewOrg(OrganisationLevel.RetailPoint);
        var userName = await CreateAccountAsync(here.Id, elsewhere.Id);
        var lensSetId = SeedLensSetAssignedTo(here.Id);

        var (atHere, _) = await SignInAsync(userName, preferredLocationId: here.Id);
        var (atElsewhere, _) = await SignInAsync(userName, preferredLocationId: elsewhere.Id);

        Assert.Contains(lensSetId, await LensSetIdsAsync(atHere));
        Assert.DoesNotContain(lensSetId, await LensSetIdsAsync(atElsewhere));
    }

    // --- Re-validated on every request ----------------------------------------------------------

    [Fact]
    public async Task ACurrentLocationWhoseAssignmentIsRemoved_SeesNoScopedRowsOnTheNextRequest()
    {
        var outlet = NewOrg(OrganisationLevel.RetailPoint);
        var userName = await CreateAccountAsync(outlet.Id);
        var (lead, _) = SeedLead(outlet.Path);
        var lensSetId = SeedLensSetAssignedTo(outlet.Id);
        var (client, _) = await SignInAsync(userName);
        Assert.Contains(lead, await LeadIdsAsync(client));

        await ChangeInDatabaseAsync(db => db.UserOrgAssignments.RemoveRange(db.UserOrgAssignments.Where(a => a.OrgNodeId == outlet.Id)));

        // Still authenticated — an invalid location narrows the request, it doesn't fail it.
        Assert.DoesNotContain(lead, await LeadIdsAsync(client));
        Assert.DoesNotContain(lensSetId, await LensSetIdsAsync(client));
    }

    [Fact]
    public async Task ACurrentLocationThatIsDeactivated_SeesNoScopedRowsOnTheNextRequest()
    {
        var outlet = NewOrg(OrganisationLevel.RetailPoint);
        var userName = await CreateAccountAsync(outlet.Id);
        var (lead, _) = SeedLead(outlet.Path);
        var (client, _) = await SignInAsync(userName);
        Assert.Contains(lead, await LeadIdsAsync(client));

        await ChangeInDatabaseAsync(db => db.OrganisationNodes.IgnoreQueryFilters().Single(o => o.Id == outlet.Id).IsDeleted = true);

        Assert.DoesNotContain(lead, await LeadIdsAsync(client));
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private sealed record Org(Guid Id, string Path, string Name);

    /// <summary>A fresh org under <paramref name="parentPath"/> (Kenya's reseller by default), at
    /// a path segment high enough not to meet any other test's.</summary>
    private Org NewOrg(OrganisationLevel level, string parentPath = OrganisationSeedConfiguration.KenyaRetailerPath, bool deactivated = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var parentId = db.OrganisationNodes.IgnoreQueryFilters().Single(o => o.HierarchyPath == parentPath).Id;
        var node = new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Name = $"Location {Guid.NewGuid():N}",
            Level = level,
            HierarchyPath = $"{parentPath}{Random.Shared.Next(10_000_000, 99_999_999)}/",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsDeleted = deactivated,
        };
        db.OrganisationNodes.Add(node);
        db.SaveChanges();
        return new Org(node.Id, node.HierarchyPath, node.Name);
    }

    /// <summary>A User-role account with a password and the given direct assignments.</summary>
    private async Task<string> CreateAccountAsync(params Guid[] assignedOrgIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"field-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = userName, Email = userName, EmailConfirmed = true };
        var created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        Assert.True((await users.AddToRoleAsync(user, RoleNames.User)).Succeeded);

        db.UserOrgAssignments.AddRange(assignedOrgIds.Select(orgId => new UserOrgAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrgNodeId = orgId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        }));
        await db.SaveChangesAsync();
        return userName;
    }

    private async Task<(HttpClient Client, LoginResponse Login)> SignInAsync(string userName, Guid? preferredLocationId = null)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = Password,
            PreferredLocationId = preferredLocationId,
        });
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        return (WithToken(login.AccessToken), login);
    }

    private HttpClient WithToken(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>An open Lead, and its Customer, recorded at <paramref name="hierarchyPath"/>.</summary>
    private (Guid LeadId, Customer Customer) SeedLead(string hierarchyPath)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            FullName = $"Customer {Guid.NewGuid():N}",
            PhoneNumber = "0700111222",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            TechnicianUserId = Guid.NewGuid(),
            CustomerId = customer.Id,
            ConsentGiven = true,
            ReasonNotPurchasedRefId = db.ReferenceDataItems.First(x => x.Category == ReferenceDataCategory.ReasonNotPurchased && x.IsActive).Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Customers.Add(customer);
        db.Leads.Add(lead);
        db.SaveChanges();
        return (lead.Id, customer);
    }

    /// <summary>A lens set with one sellable lens power, assigned only to <paramref name="orgNodeId"/>.</summary>
    private Guid SeedLensSetAssignedTo(Guid orgNodeId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var sellable = db.LensStrengthCoatingOptions.First();
        var lensSet = new PresetCatalogue { Id = Guid.NewGuid(), Name = $"Location Readers {Guid.NewGuid():N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId };
        db.PresetCatalogues.Add(lensSet);
        db.LensOptions.Add(new LensOption { Id = Guid.NewGuid(), PresetCatalogueId = lensSet.Id, LensStrengthRefId = sellable.LensStrengthRefId, SortOrder = 0 });
        db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSet.Id, OrgNodeId = orgNodeId });
        db.SaveChanges();
        return lensSet.Id;
    }

    private async Task ChangeInDatabaseAsync(Action<DotGlassesDbContext> change)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }

    private static string MatchUrl(Customer customer) =>
        $"api/v1/leads/match?fullName={Uri.EscapeDataString(customer.FullName)}&phoneNumber={Uri.EscapeDataString(customer.PhoneNumber!)}";

    private static async Task<List<Guid>> LeadIdsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<LeadDto>>("api/v1/leads"))!.Select(l => l.Id).ToList();

    private static async Task<List<Guid>> LensSetIdsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<PresetCatalogueDto>>("api/v1/preset-catalogues"))!.Select(c => c.Id).ToList();
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DotGlasses.Application.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests;

/// <summary>
/// The Test/Lead/Sale create endpoints refuse whatever the client sends unless the caller's
/// current location is an active retail point they're directly assigned to (ticket 07, spec.md
/// "Recording") — naming the reason, since the Field App's outbox surfaces it verbatim on Failed
/// records. Every non-Valid CurrentLocationStatus is exercised at least once, generally through
/// the Tests endpoint (the cheapest valid body — the check runs before ConsultationRules, so an
/// otherwise-incomplete body still reaches it); LeadsController/SalesController wiring the
/// identical guard is pinned separately at the bottom rather than repeating every reason for all
/// three endpoints.
///
/// Each client here builds its own JWT directly (rather than through /api/v1/auth/login) so a
/// status login can never itself produce — NotRetailPoint, Deactivated, an indirect assignment —
/// can be exercised the way a stale or tampered device token would reach the server.
/// </summary>
[Collection(WebApiCollection.Name)]
public class RecordingLocationApiTests(CustomWebApplicationFactory factory)
{
    private const string Password = "TestPassw0rd!";

    [Fact]
    public async Task NoCurrentLocation_IsRefusedAskingToChooseARetailPoint()
    {
        var (client, _) = await ClientWithLocationAsync([], currentLocationId: null);

        var response = await client.PostAsJsonAsync("api/v1/tests", MinimalTest());

        await AssertFormLevelRefusalAsync(response, "Choose a retail point before recording.");
    }

    [Fact]
    public async Task ACurrentLocationAboveRetailPointLevel_IsRefusedAskingToChooseARetailPoint()
    {
        var country = NewOrg(OrganisationLevel.Intermediate);
        var (client, _) = await ClientWithLocationAsync([country.Id], country.Id);

        var response = await client.PostAsJsonAsync("api/v1/tests", MinimalTest());

        await AssertFormLevelRefusalAsync(response, "Choose a retail point before recording.");
    }

    [Fact]
    public async Task ARetailPointAssignedOnlyIndirectly_IsRefusedAsNoLongerAssigned()
    {
        var retailer = NewOrg(OrganisationLevel.Intermediate);
        var outlet = NewOrg(OrganisationLevel.RetailPoint, retailer.Path);
        var (client, _) = await ClientWithLocationAsync([retailer.Id], outlet.Id);

        var response = await client.PostAsJsonAsync("api/v1/tests", MinimalTest());

        await AssertFormLevelRefusalAsync(response, $"You're no longer assigned to {outlet.Name} — ask your admin.");
    }

    [Fact]
    public async Task ARetailPointAssignmentRemovedAfterTheTokenWasIssued_IsRefusedAsNoLongerAssigned()
    {
        var outlet = NewOrg(OrganisationLevel.RetailPoint);
        var (client, userId) = await ClientWithLocationAsync([outlet.Id], outlet.Id);

        // Still authenticated — losing the assignment narrows the request, it doesn't fail it
        // (same re-validation FieldAppCurrentLocationApiTests pins for scoped reads).
        RemoveAssignment(userId, outlet.Id);

        var response = await client.PostAsJsonAsync("api/v1/tests", MinimalTest());

        await AssertFormLevelRefusalAsync(response, $"You're no longer assigned to {outlet.Name} — ask your admin.");
    }

    [Fact]
    public async Task ADeactivatedRetailPoint_IsRefusedNamingIt()
    {
        var outlet = NewOrg(OrganisationLevel.RetailPoint, deactivated: true);
        var (client, _) = await ClientWithLocationAsync([outlet.Id], outlet.Id);

        var response = await client.PostAsJsonAsync("api/v1/tests", MinimalTest());

        await AssertFormLevelRefusalAsync(response, $"{outlet.Name} has been deactivated.");
    }

    [Fact]
    public async Task ARecordAtATrainingOrgRetailPoint_IsStillAccepted()
    {
        var outlet = NewOrg(OrganisationLevel.RetailPoint, isTrainingOrg: true);
        var (client, _) = await ClientWithLocationAsync([outlet.Id], outlet.Id);

        var response = await client.PostAsJsonAsync("api/v1/tests", MinimalTest());

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    // --- The same guard on Leads and Sales, not just Tests --------------------------------------

    [Fact]
    public async Task ALeadWithNoCurrentLocation_IsRefused()
    {
        var (client, _) = await ClientWithLocationAsync([], currentLocationId: null);

        var response = await client.PostAsJsonAsync("api/v1/leads", MinimalLead());

        await AssertFormLevelRefusalAsync(response, "Choose a retail point before recording.");
    }

    [Fact]
    public async Task ASaleWithNoCurrentLocation_IsRefused()
    {
        var (client, _) = await ClientWithLocationAsync([], currentLocationId: null);

        var response = await client.PostAsJsonAsync("api/v1/sales", MinimalSale());

        await AssertFormLevelRefusalAsync(response, "Choose a retail point before recording.");
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private sealed record Org(Guid Id, string Path, string Name);

    /// <summary>A fresh org under <paramref name="parentPath"/> (Kenya's reseller by default), at
    /// a path segment high enough not to meet any other test's.</summary>
    private Org NewOrg(
        OrganisationLevel level,
        string parentPath = OrganisationSeedConfiguration.KenyaRetailerPath,
        bool deactivated = false,
        bool isTrainingOrg = false)
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
            IsTrainingOrg = isTrainingOrg,
        };
        db.OrganisationNodes.Add(node);
        db.SaveChanges();
        return new Org(node.Id, node.HierarchyPath, node.Name);
    }

    /// <summary>A User-role account directly assigned to <paramref name="assignedOrgIds"/>,
    /// holding a JWT whose current-location claim names <paramref name="currentLocationId"/> —
    /// which need not be one of the assignments, or present at all. Built directly rather than
    /// through /api/v1/auth/login, which only ever issues a Valid or NoLocation token: every other
    /// CurrentLocationStatus (NotRetailPoint, Deactivated, an indirect assignment) has to be
    /// produced this way, the way a stale or tampered device token would reach the server.</summary>
    private async Task<(HttpClient Client, Guid UserId)> ClientWithLocationAsync(Guid[] assignedOrgIds, Guid? currentLocationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"recording-{Guid.NewGuid():N}@test.local";
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

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, RoleNames.User),
        ];
        if (currentLocationId is { } locationId)
        {
            claims.Add(new(DotGlassesClaimTypes.CurrentLocationId, locationId.ToString()));
        }

        var (token, _) = factory.Services.GetRequiredService<IJwtTokenService>().CreateToken(claims);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, user.Id);
    }

    private void RemoveAssignment(Guid userId, Guid orgId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        db.UserOrgAssignments.RemoveRange(db.UserOrgAssignments.Where(a => a.UserId == userId && a.OrgNodeId == orgId));
        db.SaveChanges();
    }

    /// <summary>Id alone is enough: the current-location guard returns before ConsultationRules
    /// ever runs, so an otherwise-incomplete body still reaches every refusal test above. Only
    /// the "still accepted" test needs this to be genuinely valid, which it is — Gender/Outcome
    /// default to their zero members and nothing else is required with no lens range chosen.</summary>
    private static CreateTestRequest MinimalTest() => new() { Id = Guid.NewGuid() };

    private static CreateLeadRequest MinimalLead() => new() { Id = Guid.NewGuid() };

    private static CreateSaleRequest MinimalSale() => new() { Id = Guid.NewGuid() };

    /// <summary>The ValidationProblemDetails body's one form-level ("") message.</summary>
    private static async Task AssertFormLevelRefusalAsync(HttpResponseMessage response, string expectedMessage)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var messages = body.RootElement.GetProperty("errors").GetProperty(string.Empty);
        Assert.Equal(expectedMessage, Assert.Single(messages.EnumerateArray()).GetString());
    }
}

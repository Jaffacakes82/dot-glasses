using System.Net;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence.Configurations;

namespace DotGlasses.Web.Tests.DomainRejections;

/// <summary>
/// The server-rendered half of DomainRuleViolationFilter (ADR-0003), one rejection per service
/// that has one: the POST comes back as a redirect to the screen the admin was on, and the
/// service's own copy is rendered there — no controller catches anything, and no action reaches
/// the generic error page. Driven over real HTTP (antiforgery token and all) rather than against
/// the filter in isolation, because "the message reaches the user on the screen they were on" is
/// the behaviour that matters and a filter unit test can't show it.
///
/// Rides on OrganisationSeedConfiguration's HasData tree (DGI → Kenya → Kangemi Vision Centre →
/// Outreach Post), which CustomWebApplicationFactory's EnsureCreated() already applies — no test
/// needs an org tree of its own, and none of these rejections mutate one.
/// </summary>
public class DomainRuleViolationScreenTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    [Fact]
    public async Task ReferenceData_PairingACoatingWithItself_ShowsTheMessageOnTheReferenceDataScreen()
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/ReferenceData");
        var coatingId = Guid.NewGuid().ToString();

        var (redirect, html) = await AdminPortalFactory.PostAndFollowAsync(
            client,
            "/ReferenceData/AddCoatingPairing",
            AdminPortalFactory.Form(token, ("triggerCoatingRefId", coatingId), ("pairedCoatingRefId", coatingId)),
            referer: "/ReferenceData");

        Assert.Equal("/ReferenceData", redirect.Headers.Location?.ToString());
        Assert.Contains("A coating can&#x27;t pair with itself.", html);
    }

    [Fact]
    public async Task Organisations_DeactivatingANodeWithChildren_ShowsTheMessageOnTheOrganisationsScreen()
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Organisations");

        var (redirect, html) = await AdminPortalFactory.PostAndFollowAsync(
            client,
            "/Organisations/SetActive",
            AdminPortalFactory.Form(token, ("id", OrganisationSeedConfiguration.KenyaId.ToString()), ("value", "false")),
            referer: $"/Organisations?selectedId={OrganisationSeedConfiguration.KenyaId}");

        // The Referer carries the selected node through the redirect — the admin lands back on
        // the same node they were looking at, not on a bare Index.
        Assert.Contains($"selectedId={OrganisationSeedConfiguration.KenyaId}", redirect.Headers.Location?.ToString());
        Assert.Contains("Deactivate this node&#x27;s child orgs first", html);
    }

    [Fact]
    public async Task Organisations_UnassigningAUsersPrimaryOrg_ShowsTheMessageOnTheOrganisationsScreen()
    {
        var user = await factory.SeedUserAsync(
            $"primary-{Guid.NewGuid():N}@example.test",
            OrganisationSeedConfiguration.KenyaRetailPointId,
            OrganisationSeedConfiguration.KenyaRetailPointPath);

        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Organisations");

        var (_, html) = await AdminPortalFactory.PostAndFollowAsync(
            client,
            "/Organisations/UnassignUser",
            AdminPortalFactory.Form(
                token,
                ("orgNodeId", OrganisationSeedConfiguration.KenyaRetailPointId.ToString()),
                ("userId", user.Id.ToString())),
            referer: "/Organisations");

        Assert.Contains("Can&#x27;t un-assign a user&#x27;s primary org", html);
    }

    [Fact]
    public async Task Catalogues_CreatingACatalogueOwnedByARetailPoint_ShowsTheMessageOnThePresetCataloguesScreen()
    {
        // Country level satisfies PresetCatalogue.Manage, but the acting user's own org node is a
        // RetailPoint — the service defends the rule itself rather than trusting the claim.
        var client = factory.CreateAdminClient(OrganisationLevel.Country, OrganisationSeedConfiguration.KenyaRetailPointId);
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Catalogues");

        var (_, html) = await AdminPortalFactory.PostAndFollowAsync(
            client,
            "/Catalogues/CreateCatalogue",
            AdminPortalFactory.Form(token, ("Name", "Rejected range"), ("Kind", nameof(PresetCatalogueKind.Other))),
            referer: "/Catalogues");

        Assert.Contains("A PresetCatalogue&#x27;s owning org must be Dgi or Country level.", html);
    }

    /// <summary>Also covers the no-Referer path: the filter falls back to Index on the same
    /// controller rather than dropping the admin somewhere generic.</summary>
    [Fact]
    public async Task CustomOrders_AdvancingAFulfilledOrder_ShowsTheMessageOnTheCustomOrdersScreen()
    {
        var saleId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        factory.Seed(dbContext =>
        {
            dbContext.Customers.Add(new Customer
            {
                Id = customerId,
                HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath,
                FullName = "Asha Otieno",
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            dbContext.Sales.Add(new Sale
            {
                Id = saleId,
                HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath,
                TechnicianUserId = Guid.NewGuid(),
                CustomerId = customerId,
                LensRangeType = LensRangeType.Custom,
                OrderFromDotGlasses = true,
                FulfilmentStatus = FulfilmentStatus.Fulfilled,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        });

        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/CustomOrders");

        var (redirect, html) = await AdminPortalFactory.PostAndFollowAsync(
            client,
            "/CustomOrders/AdvanceStatus",
            AdminPortalFactory.Form(token, ("saleId", saleId.ToString())));

        Assert.Equal("/CustomOrders", redirect.Headers.Location?.ToString());
        Assert.Contains("This custom order is already Fulfilled.", html);
    }

    /// <summary>A sale the caller can't see and a sale that doesn't exist are the same fact here —
    /// the hierarchy filter hides the former, so the service cannot tell them apart and must not
    /// try, or the screen would leak which sales exist elsewhere in the tree. Both get the one
    /// sentence CustomOrderService has for it, rather than the generic error page an unhandled
    /// missing row would produce.</summary>
    [Fact]
    public async Task CustomOrders_AdvancingASaleTheCallerCannotSee_ShowsTheSameMessageAsOneThatDoesNotExist()
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/CustomOrders");

        var (_, html) = await AdminPortalFactory.PostAndFollowAsync(
            client,
            "/CustomOrders/AdvanceStatus",
            AdminPortalFactory.Form(token, ("saleId", Guid.NewGuid().ToString())),
            referer: "/CustomOrders");

        Assert.Contains("This custom order is no longer available.", html);
    }

    /// <summary>The other half of the seam: a missing row that no service has copy for keeps its
    /// InvalidOperationException and is never dressed up as a message to the user (ADR-0003) —
    /// "sequence contains no elements" must not reach a screen. Un-assigning an unknown user hits
    /// UserAdminService's bare `?? throw new InvalidOperationException("User not found.")`, which
    /// is a tampered form or a bug, not a sentence an admin should be shown.</summary>
    [Fact]
    public async Task Organisations_UnassigningAnUnknownUser_IsNotTurnedIntoAUserFacingMessage()
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Organisations");

        var response = await client.PostAsync(
            "/Organisations/UnassignUser",
            AdminPortalFactory.Form(
                token,
                ("orgNodeId", OrganisationSeedConfiguration.KenyaRetailPointId.ToString()),
                ("userId", Guid.NewGuid().ToString())));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}

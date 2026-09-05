using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Custom Orders groups by Retailer, and Retailer means one thing across the product: the nearest
/// Intermediate-level ancestor of the order's retail point (CONTEXT.md). This screen used to
/// resolve it as the retail point's immediate parent instead, which reported the country as the
/// retailer whenever a retail point hung directly off a Country — the two definitions agree
/// everywhere else, which is why it survived unnoticed.
///
/// The grouping rule itself is pure and pinned without a database in OrgTreeLookupTests. What
/// needs Postgres is the half that only appears wired to the real query filter: that naming an
/// ancestor keeps working for a caller scoped *beneath* it. That is CLAUDE.md's standing gotcha —
/// a plain scoped OrganisationNodes query only ever returns the caller's own subtree, so it
/// cannot see the caller's own Retailer, and the implementation this replaced used exactly such a
/// query. Under the in-memory provider the filter is evaluated in C# rather than SQL, so this is
/// only a real test where the application actually runs it.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CustomOrderRetailerGroupingTests(PostgresContainerFixture postgres)
{
    /// <summary>A RetailPoint hanging directly off Kenya, alongside the seeded tree's
    /// Kenya -> Kangemi Vision Centre -> Outreach Post. Segment 5 continues the seed's run.</summary>
    private const string CountryDirectOutletPath = "/1/2/5/";

    /// <summary>A path no organisation node sits on — the "we cannot say" case, which has to stay
    /// distinct from "there genuinely is no Retailer".</summary>
    private const string OrphanedPath = "/1/2/9/";

    private static DotGlassesDbContext CreateContext(string connectionString, string hierarchyPathPrefix = "") =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, hierarchyPathPrefix));

    private static CustomOrderService CreateService(DotGlassesDbContext context) =>
        new(context, new UnscopedReportQueryService(context));

    private static async Task SeedCountryDirectOutletAsync(string connectionString)
    {
        await using var seedContext = CreateContext(connectionString);

        seedContext.OrganisationNodes.Add(new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = OrganisationSeedConfiguration.KenyaId,
            Name = "Nairobi Direct Outlet",
            Level = OrganisationLevel.RetailPoint,
            HierarchyPath = CountryDirectOutletPath,
        });

        await seedContext.SaveChangesAsync();
    }

    private static async Task SeedCustomOrderAsync(string connectionString, string hierarchyPath)
    {
        await using var seedContext = CreateContext(connectionString);

        seedContext.Sales.Add(new Sale
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            TechnicianUserId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            FulfilmentStatus = FulfilmentStatus.Submitted,
        });

        await seedContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ARetailPointUnderAReseller_GroupsUnderThatReseller()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedCustomOrderAsync(connectionString, OrganisationSeedConfiguration.KenyaRetailPointPath);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath);
        var result = await CreateService(context).ListGroupedAsync(status: null);

        var retailer = Assert.Single(result.Retailers);
        Assert.Equal("Kangemi Vision Centre", retailer.RetailerName);
        Assert.Equal(OrganisationSeedConfiguration.KenyaRetailerId, retailer.RetailerId);
        Assert.Equal("Kangemi Vision Centre — Outreach Post", Assert.Single(retailer.RetailPoints).RetailPointName);
    }

    [Fact]
    public async Task ARetailPointDirectlyUnderACountry_ReportsNoRetailerRatherThanTheCountry()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedCountryDirectOutletAsync(connectionString);
        await SeedCustomOrderAsync(connectionString, CountryDirectOutletPath);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath);
        var result = await CreateService(context).ListGroupedAsync(status: null);

        // The bug this replaced: the outlet's immediate parent is Kenya, so the group was headed
        // with the country's own name as though the country were a reseller.
        var retailer = Assert.Single(result.Retailers);
        Assert.Equal(OrgTreeLookup.NoRetailer, retailer.RetailerName);
        Assert.Equal("Nairobi Direct Outlet", Assert.Single(retailer.RetailPoints).RetailPointName);
    }

    [Fact]
    public async Task HavingNoRetailerAndNotBeingInTheTree_AreSeparateGroups()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedCountryDirectOutletAsync(connectionString);
        await SeedCustomOrderAsync(connectionString, CountryDirectOutletPath);
        await SeedCustomOrderAsync(connectionString, OrphanedPath);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath);
        var result = await CreateService(context).ListGroupedAsync(status: null);

        // Both carry no Retailer node, so both report RetailerId = Guid.Empty — but they are
        // different facts and must not share one heading, which is what the old implementation's
        // single "Unknown retailer" bucket did to them.
        Assert.Equal(2, result.Retailers.Count);
        Assert.Contains(result.Retailers, r => r.RetailerName == OrgTreeLookup.NoRetailer);
        Assert.Contains(result.Retailers, r => r.RetailerName == OrgTreeLookup.UnknownRetailer);
        Assert.All(result.Retailers, r => Assert.Equal(1, r.ActiveCount));
    }

    [Fact]
    public async Task TheRetailer_IsNamedEvenForACallerScopedBeneathIt()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedCustomOrderAsync(connectionString, OrganisationSeedConfiguration.KenyaRetailPointPath);

        // Scoped at the retail point itself: the hierarchy filter shows this caller exactly one
        // organisation node — their own. Their Retailer and country sit above them, and a scoped
        // query can never return either, so this passes only because resolution goes through
        // IUnscopedReportQueryService.
        await using var context = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.KenyaRetailPointPath);
        var result = await CreateService(context).ListGroupedAsync(status: null);

        var retailer = Assert.Single(result.Retailers);
        Assert.Equal("Kangemi Vision Centre", retailer.RetailerName);
        Assert.Equal("Kangemi Vision Centre — Outreach Post", Assert.Single(retailer.RetailPoints).RetailPointName);
    }
}

using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Top Retailers/Top Countries must not rank a row OrgTreeLookup can't meaningfully attribute to a
/// real retailer or country — a DGI-level sale (no Country ancestor at all) or a sale at a path no
/// organisation node sits on. Before this, both showed up as ranked entries under "No retailer" /
/// "Unknown country", including the #1 highlight when they happened to have the most sales — the
/// resolution itself was already honest (see OrgTreeLookupTests), the bug was letting that honest
/// "we can't say" answer compete for a leaderboard slot. Every other Dashboard number (totals,
/// conversion, Top Outlets, Top Technicians) is unaffected — see ticket 03 of
/// .scratch/offline-status-and-dashboard-scoping-2026-09-09 for the decision (asked directly, not
/// assumed) to exclude rather than relabel.
/// </summary>
[Collection(PostgresCollection.Name)]
public class DashboardTopPerformingTests(PostgresContainerFixture postgres)
{
    /// <summary>A RetailPoint hanging directly off Kenya — has a Country but no Retailer.</summary>
    private const string CountryDirectOutletPath = "/1/2/5/";

    /// <summary>A path no organisation node sits on.</summary>
    private const string OrphanedPath = "/1/2/9/";

    private static DotGlassesDbContext CreateContext(string connectionString, string hierarchyPathPrefix = "") =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, hierarchyPathPrefix));

    private static DashboardQueryService CreateService(DotGlassesDbContext context) =>
        new(context, new UnscopedReportQueryService(context));

    private static async Task SeedSaleAsync(string connectionString, string hierarchyPath)
    {
        await using var seedContext = CreateContext(connectionString);
        seedContext.Sales.Add(new Sale
        {
            Id = Guid.NewGuid(),
            HierarchyPath = hierarchyPath,
            TechnicianUserId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
        });
        await seedContext.SaveChangesAsync();
    }

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

    [Fact]
    public async Task ADgiLevelSale_IsExcludedFromTopRetailersAndTopCountries()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedSaleAsync(connectionString, OrganisationSeedConfiguration.DgiPath);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath);
        var snapshot = await CreateService(context).GetAsync(null, null);

        Assert.Empty(snapshot.TopRetailers);
        Assert.Empty(snapshot.TopCountries);

        // Still counted everywhere else — this ticket only touches the two ranking widgets.
        Assert.Equal(1, snapshot.StandardSales);
        var outlet = Assert.Single(snapshot.TopOutlets);
        Assert.Equal("DOT Glasses International", outlet.Name);
    }

    [Fact]
    public async Task ARetailPointDirectlyUnderACountry_IsExcludedFromTopRetailersButKeptInTopCountries()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedCountryDirectOutletAsync(connectionString);
        await SeedSaleAsync(connectionString, CountryDirectOutletPath);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath);
        var snapshot = await CreateService(context).GetAsync(null, null);

        Assert.Empty(snapshot.TopRetailers);

        var country = Assert.Single(snapshot.TopCountries);
        Assert.Equal("Kenya", country.Name);
        Assert.Equal(1, country.Sales);
    }

    /// <summary>OrphanedPath ("/1/2/9/") names no real node, but it still nests under Kenya's own
    /// path — CountryName resolves by path-prefix ancestry, which needs a Country node *above* the
    /// row, not an exact node *at* it, so it still honestly names "Kenya" and belongs in Top
    /// Countries. Only the retailer side is genuinely unknown here (no Intermediate node anywhere
    /// in "/1/2/9/"'s ancestry, and no exact node at the path itself either).</summary>
    [Fact]
    public async Task ASaleAtAnOrphanedPath_IsExcludedFromTopRetailersButKeptInTopCountriesByAncestry()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedSaleAsync(connectionString, OrphanedPath);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath);
        var snapshot = await CreateService(context).GetAsync(null, null);

        Assert.Empty(snapshot.TopRetailers);
        Assert.Equal("Kenya", Assert.Single(snapshot.TopCountries).Name);
        Assert.Equal(1, snapshot.StandardSales);
    }


    [Fact]
    public async Task ANormalRetailPointSale_AppearsInOutletsRetailersAndCountries()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedSaleAsync(connectionString, OrganisationSeedConfiguration.KenyaRetailPointPath);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath);
        var snapshot = await CreateService(context).GetAsync(null, null);

        Assert.Equal("Kangemi Vision Centre — Outreach Post", Assert.Single(snapshot.TopOutlets).Name);
        Assert.Equal("Kangemi Vision Centre", Assert.Single(snapshot.TopRetailers).Name);
        Assert.Equal("Kenya", Assert.Single(snapshot.TopCountries).Name);
    }
}

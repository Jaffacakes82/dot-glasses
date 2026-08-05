using DotGlasses.Application.Dashboard;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — matches
/// EventHistoryQueryService/CustomOrderService (bespoke reporting reads, no repository interface
/// needed for this shape). Org name/level resolution goes through IUnscopedReportQueryService,
/// not a plain scoped OrganisationNodes query — a caller scoped at RetailPoint level can never
/// see their own Country/Intermediate ancestors via the standard hierarchy filter (it only ever
/// shows a caller their own subtree), so a plain query would silently resolve every
/// outlet/retailer/country name to "Unknown" for anyone below Country level. Same bug class
/// PresetCatalogueQueryService hit earlier this session, see CLAUDE.md.</summary>
public class DashboardQueryService(DotGlassesDbContext dbContext, IUnscopedReportQueryService unscopedReportQueryService) : IDashboardQueryService
{
    private const int TopN = 5;
    private const int TrendBuckets = 6;
    private static readonly TimeSpan BucketWidth = TimeSpan.FromDays(7);

    public async Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var orgLookup = new OrgLookup(await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken));

        var tests = (await dbContext.Tests.ToListAsync(cancellationToken))
            .Where(t => !orgLookup.IsUnderTrainingOrg(t.HierarchyPath))
            .ToList();
        var leads = (await dbContext.Leads.ToListAsync(cancellationToken))
            .Where(l => !orgLookup.IsUnderTrainingOrg(l.HierarchyPath))
            .ToList();
        var sales = (await dbContext.Sales.ToListAsync(cancellationToken))
            .Where(s => !orgLookup.IsUnderTrainingOrg(s.HierarchyPath))
            .ToList();

        var leadsById = leads.ToDictionary(l => l.Id);

        bool TestConvertedToSale(Test t) =>
            t.ConvertedToLeadId is { } leadId && leadsById.TryGetValue(leadId, out var lead) && lead.SaleId is not null;

        var pendingLeads = leads.Count(l => !l.ConvertedFlag);
        var customOrders = sales.Count(s => s.FulfilmentStatus is not null);
        var standardSales = sales.Count - customOrders;
        var referralsLogged = tests.Count(t => t.Outcome == TestOutcome.Referred);

        var testToSaleConversion = ConversionPercent(tests, TestConvertedToSale);
        var neededTests = tests.Where(t => t.Outcome == TestOutcome.NeedsGlasses).ToList();
        var neededToSaleConversion = ConversionPercent(neededTests, TestConvertedToSale);

        var maleCount = tests.Count(t => t.Gender == Gender.Male);
        var femaleCount = tests.Count(t => t.Gender == Gender.Female);
        var genderTotal = maleCount + femaleCount;
        var genderMalePercent = genderTotal == 0 ? 0 : (int)Math.Round(100.0 * maleCount / genderTotal);
        var genderFemalePercent = genderTotal == 0 ? 0 : 100 - genderMalePercent;

        var trend = BuildTrend(tests, TestConvertedToSale);

        var technicianNames = await dbContext.Users
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.UserName ?? "—" : u.FullName, cancellationToken);

        return new DashboardSnapshot(
            pendingLeads,
            tests.Count,
            standardSales,
            customOrders,
            testToSaleConversion,
            neededToSaleConversion,
            referralsLogged,
            trend,
            genderMalePercent,
            genderFemalePercent,
            RankByKey(sales, tests, s => orgLookup.Outlet(s.HierarchyPath), t => orgLookup.Outlet(t.HierarchyPath)),
            RankByKey(sales, tests, s => orgLookup.Retailer(s.HierarchyPath), t => orgLookup.Retailer(t.HierarchyPath)),
            RankByKey(sales, tests, s => orgLookup.Country(s.HierarchyPath), t => orgLookup.Country(t.HierarchyPath)),
            RankByKey(sales, tests, s => technicianNames.GetValueOrDefault(s.TechnicianUserId, "—"), t => technicianNames.GetValueOrDefault(t.TechnicianUserId, "—")));
    }

    private static double ConversionPercent<T>(IReadOnlyCollection<T> population, Func<T, bool> converted) =>
        population.Count == 0 ? 0 : 100.0 * population.Count(converted) / population.Count;

    private static IReadOnlyList<int> BuildTrend(IReadOnlyList<Test> tests, Func<Test, bool> testConvertedToSale)
    {
        var now = DateTimeOffset.UtcNow;
        var buckets = new List<int>();

        for (var i = TrendBuckets - 1; i >= 0; i--)
        {
            var bucketEnd = now - i * BucketWidth;
            var bucketStart = bucketEnd - BucketWidth;
            var bucketTests = tests.Where(t => t.CreatedAtUtc >= bucketStart && t.CreatedAtUtc < bucketEnd).ToList();
            buckets.Add((int)Math.Round(ConversionPercent(bucketTests, testConvertedToSale)));
        }

        return buckets;
    }

    /// <summary>Groups Sales and Tests by the same key (outlet/retailer/country/technician),
    /// ranks by sales volume descending, and pairs each with its own conversion % (that key's
    /// share of Tests that became a Sale) — top 5.</summary>
    private static IReadOnlyList<DashboardRankedEntry> RankByKey(
        IReadOnlyList<Sale> sales, IReadOnlyList<Test> tests, Func<Sale, string> saleKey, Func<Test, string> testKey)
    {
        var testCountsByKey = tests.GroupBy(testKey).ToDictionary(g => g.Key, g => g.Count());

        return sales.GroupBy(saleKey)
            .Select(g =>
            {
                var testCount = testCountsByKey.GetValueOrDefault(g.Key, 0);
                var conversion = testCount == 0 ? 0 : 100.0 * g.Count() / testCount;
                return new DashboardRankedEntry(g.Key, g.Count(), Math.Round(conversion, 1));
            })
            .OrderByDescending(e => e.Sales)
            .Take(TopN)
            .ToList();
    }

    private sealed class OrgLookup(IReadOnlyList<OrganisationNodeSummary> nodes)
    {
        private readonly Dictionary<string, OrganisationNodeSummary> _byPath = nodes.ToDictionary(n => n.HierarchyPath);
        private readonly IReadOnlyList<OrganisationNodeSummary> _countries = nodes.Where(n => n.Level == OrganisationLevel.Country).ToList();
        private readonly IReadOnlyList<OrganisationNodeSummary> _intermediates = nodes.Where(n => n.Level == OrganisationLevel.Intermediate).ToList();
        private readonly IReadOnlyList<string> _trainingOrgPaths = nodes.Where(n => n.IsTrainingOrg).Select(n => n.HierarchyPath).ToList();

        public bool IsUnderTrainingOrg(string hierarchyPath) =>
            _trainingOrgPaths.Any(p => hierarchyPath.StartsWith(p, StringComparison.Ordinal));

        public string Outlet(string hierarchyPath) =>
            _byPath.TryGetValue(hierarchyPath, out var node) ? node.Name : "Unknown outlet";

        public string Country(string hierarchyPath) =>
            _countries.FirstOrDefault(c => hierarchyPath.StartsWith(c.HierarchyPath, StringComparison.Ordinal))?.Name ?? "Unknown country";

        /// <summary>Nearest Intermediate-level ancestor (the design mockup's "retailer" tier) —
        /// longest matching path prefix, since an Intermediate can itself sit under another
        /// Intermediate.</summary>
        public string Retailer(string hierarchyPath) =>
            _intermediates.Where(n => hierarchyPath.StartsWith(n.HierarchyPath, StringComparison.Ordinal))
                .OrderByDescending(n => n.HierarchyPath.Length)
                .FirstOrDefault()?.Name ?? "Unknown retailer";
    }
}

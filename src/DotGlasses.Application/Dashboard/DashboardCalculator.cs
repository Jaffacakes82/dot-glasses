using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Dashboard;

/// <summary>
/// Every figure on the MI Reporting Dashboard, worked out from rows already loaded — no database,
/// so each definition can be tested on its own. DashboardQueryService loads what the caller can
/// see and hands it here.
///
/// Three things hold for every figure. <b>Training organisations are left out</b> — a row at or
/// beneath one never counts. <b>The Country and Retailer filters narrow by where a row sits in
/// the tree</b>, asked through OrgTreeLookup. And <b>whether a Test converted is a fact about its
/// whole journey</b> (Test → Lead → Sale), judged against the full history: the date range only
/// chooses which Tests are being measured.
/// </summary>
public static class DashboardCalculator
{
    private const int TopN = 5;
    private const int TrendBuckets = 6;
    private static readonly TimeSpan BucketWidth = TimeSpan.FromDays(7);

    public static DashboardFigures Calculate(
        IReadOnlyList<Test> allTests,
        IReadOnlyList<Lead> allLeads,
        IReadOnlyList<Sale> allSales,
        IReadOnlyList<CustomOrder> allOrders,
        OrgTreeLookup orgLookup,
        IReadOnlyDictionary<Guid, string> technicianNames,
        DashboardFilter filter,
        DateTimeOffset now)
    {
        bool Counts(string hierarchyPath) => !orgLookup.IsRowUnderTrainingOrg(hierarchyPath) && filter.Includes(orgLookup, hierarchyPath);
        bool InRange(DateTimeOffset at) => (filter.FromUtc is null || at >= filter.FromUtc) && (filter.ToUtcExclusive is null || at < filter.ToUtcExclusive);

        var placedTests = allTests.Where(t => Counts(t.HierarchyPath)).ToList();
        var placedLeads = allLeads.Where(l => Counts(l.HierarchyPath)).ToList();
        var placedSales = allSales.Where(s => Counts(s.HierarchyPath)).ToList();
        var placedOrders = allOrders.Where(o => Counts(o.HierarchyPath)).ToList();

        var tests = placedTests.Where(t => InRange(t.CreatedAtUtc)).ToList();
        var leads = placedLeads.Where(l => InRange(l.CreatedAtUtc)).ToList();
        var sales = placedSales.Where(s => InRange(s.CreatedAtUtc)).ToList();

        // From every Lead the caller can see, not the filtered ones: whether a Test converted is
        // about its journey, whenever and wherever the Lead and the Sale were recorded.
        var leadsById = allLeads.ToDictionary(l => l.Id);
        bool TestConvertedToSale(Test t) =>
            t.ConvertedToLeadId is { } leadId && leadsById.TryGetValue(leadId, out var lead) && lead.SaleId is not null;

        // A custom order is counted by when it was placed, paid for or not (ADR-0008). A standard
        // sale is a Sale with no order behind it, so the two tiles never overlap — a Sale that
        // converted an ordered Lead belongs to the order's tile.
        var saleIdsWithAnOrder = allOrders.Where(o => o.SaleId is not null).Select(o => o.SaleId!.Value).ToHashSet();
        var customOrders = placedOrders.Count(o => InRange(o.PlacedAtUtc));
        var standardSales = sales.Count(s => !saleIdsWithAnOrder.Contains(s.Id));

        var neededTests = tests.Where(t => t.Outcome == TestOutcome.NeedsGlasses).ToList();

        var maleCount = tests.Count(t => t.Gender == Gender.Male);
        var femaleCount = tests.Count(t => t.Gender == Gender.Female);
        var genderTotal = maleCount + femaleCount;
        var genderMalePercent = genderTotal == 0 ? 0 : (int)Math.Round(100.0 * maleCount / genderTotal);

        IReadOnlyList<DashboardRankedEntry> Rank(Func<string, Guid, string?> key) =>
            RankByKey(tests, leads, sales, key, TestConvertedToSale, filter.Ranking);

        return new DashboardFigures(
            PendingLeads: leads.Count(l => !l.ConvertedFlag),
            TotalTests: tests.Count,
            StandardSales: standardSales,
            CustomOrders: customOrders,
            TestToSaleConversionPercent: ConversionPercent(tests, TestConvertedToSale),
            NeededToSaleConversionPercent: ConversionPercent(neededTests, TestConvertedToSale),
            ReferralsLogged: ReferredJourneys(allTests, allLeads, allSales, tests, leads, sales),
            // Always the real last six weeks, whatever the date range — but it does follow the
            // Country and Retailer filters.
            ConversionTrendPercent: BuildTrend(placedTests, TestConvertedToSale, now),
            GenderMalePercent: genderMalePercent,
            GenderFemalePercent: genderTotal == 0 ? 0 : 100 - genderMalePercent,
            TopOutlets: Rank((path, _) => orgLookup.RowOutletName(path)),
            // Unlike outlets, a row OrgTreeLookup can't honestly name a Retailer or Country for
            // ("No retailer" — it genuinely hangs directly off a Country; "Unknown" — the path
            // isn't in the tree) is left out of these two lists rather than ranked under that
            // fallback as if it were a real competing entity. It still counts everywhere else.
            TopRetailers: Rank((path, _) => orgLookup.RowRetailer(path).HasRetailer ? orgLookup.RowRetailerName(path) : null),
            TopCountries: Rank((path, _) => orgLookup.RowHasCountry(path) ? orgLookup.RowCountryName(path) : null),
            TopTechnicians: Rank((_, technicianUserId) => technicianNames.GetValueOrDefault(technicianUserId, "—")));
    }

    /// <summary>
    /// "Referrals logged" counts customer journeys, not records. A Test continued into a Lead, and
    /// a Lead converted into a Sale, are one journey (linked by Test.ConvertedToLeadId and
    /// Lead.SaleId), and "Referred or treated" is asked afresh at each step — so the same referral
    /// is often on two or three records. A journey counts once when any of its referred records
    /// falls in the range; a record with no link is a journey of its own.
    /// </summary>
    public static int ReferredJourneys(
        IReadOnlyList<Test> allTests, IReadOnlyList<Lead> allLeads, IReadOnlyList<Sale> allSales,
        IReadOnlyList<Test> testsInRange, IReadOnlyList<Lead> leadsInRange, IReadOnlyList<Sale> salesInRange)
    {
        // Each record's journey is named by its earliest step: the Test when there is one, else
        // the Lead, else the Sale itself.
        var testByLeadId = new Dictionary<Guid, Guid>();
        foreach (var test in allTests)
        {
            if (test.ConvertedToLeadId is { } leadId)
            {
                testByLeadId.TryAdd(leadId, test.Id);
            }
        }

        Guid JourneyOfLead(Guid leadId) => testByLeadId.GetValueOrDefault(leadId, leadId);

        var leadBySaleId = new Dictionary<Guid, Guid>();
        foreach (var lead in allLeads)
        {
            if (lead.SaleId is { } saleId)
            {
                leadBySaleId.TryAdd(saleId, lead.Id);
            }
        }

        Guid JourneyOfSale(Guid saleId) => leadBySaleId.TryGetValue(saleId, out var leadId) ? JourneyOfLead(leadId) : saleId;

        return testsInRange.Where(t => t.ReferredOrTreated).Select(t => t.Id)
            .Concat(leadsInRange.Where(l => l.ReferredOrTreated).Select(l => JourneyOfLead(l.Id)))
            .Concat(salesInRange.Where(s => s.ReferredOrTreated).Select(s => JourneyOfSale(s.Id)))
            .Distinct()
            .Count();
    }

    /// <summary>
    /// One "Top performing" list: Tests, Leads and Sales recorded under each key, and that key's
    /// conversion — of the Tests recorded there, the share that reached a Sale through a Lead,
    /// which is the tiles' definition applied to the key and so can never exceed 100%. (Sales ÷
    /// Tests, which this replaced, went above 100% wherever Sales were recorded without Tests.)
    /// A key with Sales and no Tests shows 0%, which is accurate: none of its Tests converted
    /// because it has none.
    ///
    /// <paramref name="key"/> returns null for a row that can't be attributed, which leaves it
    /// out of this list. Top five, by most Sales or by best conversion; ranking by conversion
    /// leaves out a key with no Tests, which has no conversion to rank.
    /// </summary>
    public static IReadOnlyList<DashboardRankedEntry> RankByKey(
        IReadOnlyList<Test> tests, IReadOnlyList<Lead> leads, IReadOnlyList<Sale> sales,
        Func<string, Guid, string?> key, Func<Test, bool> testConvertedToSale, DashboardRanking ranking)
    {
        var rows = new Dictionary<string, (int Tests, int Converted, int Leads, int Sales)>();

        void Add(string? name, int tests = 0, int converted = 0, int leadCount = 0, int saleCount = 0)
        {
            if (name is null)
            {
                return;
            }

            var row = rows.GetValueOrDefault(name);
            rows[name] = (row.Tests + tests, row.Converted + converted, row.Leads + leadCount, row.Sales + saleCount);
        }

        foreach (var test in tests)
        {
            Add(key(test.HierarchyPath, test.TechnicianUserId), tests: 1, converted: testConvertedToSale(test) ? 1 : 0);
        }

        foreach (var lead in leads)
        {
            Add(key(lead.HierarchyPath, lead.TechnicianUserId), leadCount: 1);
        }

        foreach (var sale in sales)
        {
            Add(key(sale.HierarchyPath, sale.TechnicianUserId), saleCount: 1);
        }

        var entries = rows.Select(r => new DashboardRankedEntry(
            r.Key, r.Value.Tests, r.Value.Leads, r.Value.Sales,
            r.Value.Tests == 0 ? 0 : Math.Round(100.0 * r.Value.Converted / r.Value.Tests, 1)));

        var ranked = ranking == DashboardRanking.BestConversion
            ? entries.Where(e => e.Tests > 0).OrderByDescending(e => e.ConversionPercent).ThenByDescending(e => e.Sales)
            : entries.OrderByDescending(e => e.Sales).ThenByDescending(e => e.ConversionPercent);

        return ranked.ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Take(TopN).ToList();
    }

    private static double ConversionPercent<T>(IReadOnlyCollection<T> population, Func<T, bool> converted) =>
        population.Count == 0 ? 0 : 100.0 * population.Count(converted) / population.Count;

    private static IReadOnlyList<int> BuildTrend(IReadOnlyList<Test> tests, Func<Test, bool> testConvertedToSale, DateTimeOffset now)
    {
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
}

/// <summary>What narrows the dashboard: the date range, and optionally one Country and one
/// Retailer. <see cref="NoRetailer"/> selects the retail points that hang directly off a Country
/// (CONTEXT.md: they genuinely have no Retailer). A Country or Retailer the caller can't see
/// matches none of their rows, so it shows nothing rather than anyone else's data.</summary>
public record DashboardFilter(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtcExclusive = null,
    Guid? CountryId = null,
    Guid? RetailerId = null,
    bool NoRetailer = false,
    DashboardRanking Ranking = DashboardRanking.MostSales)
{
    public bool HasOrganisationFilter => CountryId is not null || RetailerId is not null || NoRetailer;

    /// <summary>Whether a row at this path is inside the chosen Country and Retailer. A Retailer
    /// is matched by path, so choosing a distributor includes every retailer beneath it.</summary>
    public bool Includes(OrgTreeLookup orgLookup, string hierarchyPath)
    {
        if (!HasOrganisationFilter)
        {
            return true;
        }

        if (!HierarchyPath.TryParse(hierarchyPath, out var path))
        {
            return false;
        }

        if (CountryId is { } countryId && orgLookup.FindCountry(path)?.Id != countryId)
        {
            return false;
        }

        if (NoRetailer)
        {
            return orgLookup.ResolveRetailer(path).Kind == RetailerResolutionKind.NoRetailer;
        }

        return RetailerId is not { } retailerId || orgLookup.IsAtOrBeneath(path, retailerId);
    }
}

public enum DashboardRanking
{
    MostSales,
    BestConversion,
}

public record DashboardFigures(
    int PendingLeads,
    int TotalTests,
    int StandardSales,
    int CustomOrders,
    double TestToSaleConversionPercent,
    double NeededToSaleConversionPercent,
    int ReferralsLogged,
    IReadOnlyList<int> ConversionTrendPercent,
    int GenderMalePercent,
    int GenderFemalePercent,
    IReadOnlyList<DashboardRankedEntry> TopOutlets,
    IReadOnlyList<DashboardRankedEntry> TopRetailers,
    IReadOnlyList<DashboardRankedEntry> TopCountries,
    IReadOnlyList<DashboardRankedEntry> TopTechnicians);

using System.Net;
using System.Text.RegularExpressions;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Web.Tests.Reporting;

/// <summary>
/// The MI Reporting Dashboard as rendered: the Country and Retailer filters (what they offer, what
/// they narrow, and that a value outside the viewer's scope shows nothing), the two order tiles
/// (ADR-0008), the four-figure "Top performing" rows with their ranking switch, and a referral
/// counted once per journey. The definitions themselves are DashboardCalculatorTests'; these pin
/// that the screen asks for them with the right filter and shows what comes back.
///
/// Each test builds its own country, so its figures are its own whatever else is in the database.
/// </summary>
public class DashboardScreenTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private static int _nextSegment = 700;

    /// <summary>A country with one retailer (two retail points), a retail point hanging directly
    /// off the country, and a training retailer with a retail point of its own.</summary>
    private sealed record Tree(
        Guid CountryId, string CountryName, Guid RetailerId, string RetailerName,
        string OutletOne, string OutletTwo, string DirectOutlet, string TrainingOutlet,
        string OutletOneName, string OutletTwoName, string DirectOutletName);

    private Tree ACountry()
    {
        var n = Interlocked.Increment(ref _nextSegment);
        var root = $"{OrganisationSeedConfiguration.DgiPath}{n}/";
        var tree = new Tree(
            Guid.NewGuid(), $"Dashland {n}", Guid.NewGuid(), $"Dash Retail {n}",
            $"{root}1/1/", $"{root}1/2/", $"{root}3/", $"{root}4/1/",
            $"Dash Outlet One {n}", $"Dash Outlet Two {n}", $"Dash Direct {n}");
        var trainingRetailerId = Guid.NewGuid();

        factory.Seed(db => db.OrganisationNodes.AddRange(
            Node(tree.CountryId, OrganisationSeedConfiguration.DgiId, tree.CountryName, OrganisationLevel.Country, root),
            Node(tree.RetailerId, tree.CountryId, tree.RetailerName, OrganisationLevel.Intermediate, $"{root}1/"),
            Node(Guid.NewGuid(), tree.RetailerId, tree.OutletOneName, OrganisationLevel.RetailPoint, tree.OutletOne),
            Node(Guid.NewGuid(), tree.RetailerId, tree.OutletTwoName, OrganisationLevel.RetailPoint, tree.OutletTwo),
            Node(Guid.NewGuid(), tree.CountryId, tree.DirectOutletName, OrganisationLevel.RetailPoint, tree.DirectOutlet),
            Node(trainingRetailerId, tree.CountryId, $"Dash Training {n}", OrganisationLevel.Intermediate, $"{root}4/", isTraining: true),
            Node(Guid.NewGuid(), trainingRetailerId, $"Dash Training Outlet {n}", OrganisationLevel.RetailPoint, tree.TrainingOutlet)));
        return tree;
    }

    private static OrganisationNode Node(Guid id, Guid parentId, string name, OrganisationLevel level, string path, bool isTraining = false) =>
        new() { Id = id, ParentId = parentId, Name = name, Level = level, HierarchyPath = path, IsTrainingOrg = isTraining };

    private static readonly DateTimeOffset Yesterday = DateTimeOffset.UtcNow.AddDays(-1);

    private static Test ATest(string path, bool referred = false) =>
        new() { Id = Guid.NewGuid(), HierarchyPath = path, TechnicianUserId = Guid.NewGuid(), ReferredOrTreated = referred, CreatedAtUtc = Yesterday };

    private static Lead ALead(string path, Test? from = null, bool referred = false)
    {
        var lead = new Lead { Id = Guid.NewGuid(), HierarchyPath = path, TechnicianUserId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ReferredOrTreated = referred, CreatedAtUtc = Yesterday };
        if (from is not null)
        {
            lead.SourceTestId = from.Id;
            from.ConvertedToLeadId = lead.Id;
        }

        return lead;
    }

    private static Sale ASale(string path, Lead? from = null)
    {
        var sale = new Sale { Id = Guid.NewGuid(), HierarchyPath = path, TechnicianUserId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), CreatedAtUtc = Yesterday };
        if (from is not null)
        {
            sale.SourceLeadId = from.Id;
            from.SaleId = sale.Id;
            from.ConvertedFlag = true;
        }

        return sale;
    }

    private static CustomOrder AnOrder(string path, Lead? lead = null, Sale? sale = null) =>
        new() { Id = Guid.NewGuid(), HierarchyPath = path, Status = FulfilmentStatus.Submitted, PlacedAtUtc = Yesterday, LeadId = lead?.Id, SaleId = sale?.Id, CreatedAtUtc = Yesterday };

    /// <summary>Seeds a mixed bag of rows in one save.</summary>
    private void Record(params object[] rows) => factory.Seed(db => db.AddRange(rows));

    /// <summary>A whole journey at one place: a Test that became a Lead that became a Sale.</summary>
    private void RecordJourneys(string path, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var test = ATest(path);
            var lead = ALead(path, test);
            Record(test, lead, ASale(path, lead));
        }
    }

    private async Task<string> DashboardAsync(HttpClient client, string query) =>
        WebUtility.HtmlDecode(await client.GetStringAsync($"/?{query}"));

    private Task<string> DashboardAsync(string query) => DashboardAsync(factory.CreateAdminClient(), query);

    private static int Tile(string html, string label)
    {
        var match = Regex.Match(html, $@"{Regex.Escape(label)}</div>\s*<div class=""dg-stat-value""[^>]*>(\d+)");
        Assert.True(match.Success, $"No \"{label}\" tile on the dashboard.");
        return int.Parse(match.Groups[1].Value);
    }

    private sealed record Entry(string Name, int Tests, int Leads, int Sales, double Conversion);

    /// <summary>One "Top performing" list, in the order shown.</summary>
    private static List<Entry> TopList(string html, string heading)
    {
        var start = html.IndexOf($">{heading}</div>", StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        var next = html.IndexOf(">Top ", start + 1, StringComparison.Ordinal);
        var section = next < 0 ? html[start..] : html[start..next];
        return Regex.Matches(section, @"font-weight:600;"">(?<name>[^<]+)</div>\s*<div class=""dg-stat-label"">(?<t>\d+) tests · (?<l>\d+) leads · (?<s>\d+) sales · (?<c>[\d.]+)%")
            .Select(m => new Entry(m.Groups["name"].Value.Trim(), int.Parse(m.Groups["t"].Value), int.Parse(m.Groups["l"].Value), int.Parse(m.Groups["s"].Value), double.Parse(m.Groups["c"].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static List<string> Options(string html, string selectId)
    {
        var select = Regex.Match(html, $@"<select[^>]*id=""{selectId}""[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.True(select.Success, $"No #{selectId} dropdown on the dashboard.");
        return Regex.Matches(select.Groups[1].Value, @"<option[^>]*>([^<]*)</option>").Select(m => m.Groups[1].Value.Trim()).ToList();
    }

    // --- The order tiles (ADR-0008) -----------------------------------------------------------

    [Fact]
    public async Task TheOrderTiles_CountOrdersPaidOrNot_AndStandardSalesNeverOverlapThem()
    {
        var tree = ACountry();

        // Two Sales from stock; one Sale that ordered; one Lead that ordered and hasn't paid; one
        // Lead that ordered and then converted.
        var orderingSale = ASale(tree.OutletOne);
        var unpaidLead = ALead(tree.OutletOne);
        var convertedLead = ALead(tree.OutletOne);
        var convertingSale = ASale(tree.OutletOne, convertedLead);
        Record(
            ASale(tree.OutletOne), ASale(tree.OutletTwo),
            orderingSale, AnOrder(tree.OutletOne, sale: orderingSale),
            unpaidLead, AnOrder(tree.OutletOne, lead: unpaidLead),
            convertedLead, convertingSale, AnOrder(tree.OutletOne, convertedLead, convertingSale));

        // A training retail point's rows are left out of everything.
        var trainingSale = ASale(tree.TrainingOutlet);
        Record(trainingSale, AnOrder(tree.TrainingOutlet, sale: trainingSale), ASale(tree.TrainingOutlet), ATest(tree.TrainingOutlet));

        var html = await DashboardAsync($"country={tree.CountryId}");

        Assert.Equal(2, Tile(html, "Standard sales"));
        Assert.Equal(3, Tile(html, "Custom orders"));
        Assert.Equal(1, Tile(html, "Pending leads"));
        Assert.Equal(0, Tile(html, "Total tests"));
    }

    // --- Country and Retailer filters ---------------------------------------------------------

    [Fact]
    public async Task ADgiAdminFilteringToOneCountry_SeesOnlyThatCountrysFigures()
    {
        var here = ACountry();
        var elsewhere = ACountry();
        RecordJourneys(here.OutletOne, 2);
        Record(ATest(here.DirectOutlet, referred: true));
        RecordJourneys(elsewhere.OutletOne, 5);

        var html = await DashboardAsync($"country={here.CountryId}");

        Assert.Equal(3, Tile(html, "Total tests"));
        Assert.Equal(2, Tile(html, "Standard sales"));
        Assert.Equal(1, Tile(html, "Referrals logged"));
        Assert.Equal(here.CountryName, Assert.Single(TopList(html, "Top countries")).Name);
        Assert.DoesNotContain(TopList(html, "Top outlets"), e => e.Name == elsewhere.OutletOneName);

        // The chosen country stays chosen, and the tiles' links say what they don't carry.
        Assert.Matches($@"<option value=""{here.CountryId}"" selected=""selected"">", html);
        Assert.Contains("id=\"drilldownNote\"", html);
        Assert.DoesNotMatch(@"href=""/EventHistory[^""]*country=", html);
    }

    [Fact]
    public async Task WithNoOrganisationFilter_ThereIsNoNoteUnderTheTiles()
    {
        Assert.DoesNotContain("id=\"drilldownNote\"", await DashboardAsync("rank=MostSales"));
    }

    [Fact]
    public async Task ChoosingACountry_NarrowsTheRetailerChoicesToThatCountry()
    {
        var tree = ACountry();

        var all = await DashboardAsync("rank=MostSales");
        var narrowed = await DashboardAsync($"country={tree.CountryId}");

        Assert.Contains("Kangemi Vision Centre", Options(all, "retailer"));
        Assert.Contains(tree.RetailerName, Options(all, "retailer"));

        var offered = Options(narrowed, "retailer");
        Assert.Contains(tree.RetailerName, offered);
        Assert.DoesNotContain("Kangemi Vision Centre", offered);

        // The country has a retail point hanging directly off it, so "No retailer" is a choice.
        Assert.Contains("No retailer", offered);
    }

    [Fact]
    public async Task ACountryAdmin_IsOfferedOnlyTheirOwnCountryAndItsRetailers()
    {
        var mine = ACountry();
        var theirs = ACountry();
        var (client, _) = factory.CreateAdminClientWithAssignments(mine.CountryId);

        var html = await DashboardAsync(client, "rank=MostSales");

        Assert.Equal(["All countries", mine.CountryName], Options(html, "country"));

        var retailers = Options(html, "retailer");
        Assert.Contains(mine.RetailerName, retailers);
        Assert.DoesNotContain(theirs.RetailerName, retailers);
        Assert.DoesNotContain("Kangemi Vision Centre", retailers);
    }

    [Fact]
    public async Task ARetailAdmin_IsOfferedTheCountryAboveThem_ResolvedThroughTheTree()
    {
        // A caller below Country level sees no Country node of their own; the option still names
        // the country their organisations sit in.
        var tree = ACountry();
        var (client, _) = factory.CreateAdminClientWithAssignments(tree.RetailerId);

        var html = await DashboardAsync(client, "rank=MostSales");

        Assert.Equal(["All countries", tree.CountryName], Options(html, "country"));
        Assert.Equal(["All retailers", tree.RetailerName], Options(html, "retailer"));
    }

    [Fact]
    public async Task ARetailPointAdmin_IsOfferedTheRetailerAndCountryAboveThem()
    {
        var tree = ACountry();
        var outletOneId = Guid.Empty;
        factory.Seed(db => outletOneId = db.OrganisationNodes.IgnoreQueryFilters().Single(o => o.HierarchyPath == tree.OutletOne).Id);
        var (client, _) = factory.CreateAdminClientWithAssignments(outletOneId);

        var html = await DashboardAsync(client, "rank=MostSales");

        Assert.Equal(["All countries", tree.CountryName], Options(html, "country"));
        Assert.Equal(["All retailers", tree.RetailerName], Options(html, "retailer"));
    }

    [Fact]
    public async Task ARetailerFilter_NarrowsToThatRetailersRetailPoints_AndNoRetailerToTheDirectOnes()
    {
        var tree = ACountry();
        RecordJourneys(tree.OutletOne, 1);
        RecordJourneys(tree.OutletTwo, 1);
        RecordJourneys(tree.DirectOutlet, 3);

        var underRetailer = await DashboardAsync($"country={tree.CountryId}&retailer={tree.RetailerId}");
        var direct = await DashboardAsync($"country={tree.CountryId}&retailer=none");

        Assert.Equal(2, Tile(underRetailer, "Total tests"));
        Assert.Equal(
            new[] { tree.OutletOneName, tree.OutletTwoName }.Order(),
            TopList(underRetailer, "Top outlets").Select(e => e.Name).Order());

        Assert.Equal(3, Tile(direct, "Total tests"));
        Assert.Equal(tree.DirectOutletName, Assert.Single(TopList(direct, "Top outlets")).Name);
        Assert.Matches(@"<option value=""none"" selected=""selected"">No retailer</option>", direct);
    }

    [Fact]
    public async Task ACountryOrRetailerOutsideTheViewersScope_ShowsNothingRatherThanSomeoneElsesData()
    {
        var mine = ACountry();
        var theirs = ACountry();
        RecordJourneys(mine.OutletOne, 1);
        RecordJourneys(theirs.OutletOne, 4);
        var (client, _) = factory.CreateAdminClientWithAssignments(mine.CountryId);

        foreach (var query in new[] { $"country={theirs.CountryId}", $"retailer={theirs.RetailerId}", "retailer=not-a-retailer", "country=not-a-country" })
        {
            var html = await DashboardAsync(client, query);

            Assert.Equal(0, Tile(html, "Total tests"));
            Assert.Equal(0, Tile(html, "Standard sales"));
            Assert.DoesNotContain(theirs.OutletOneName, html);
            Assert.DoesNotContain(theirs.CountryName, html);
        }
    }

    // --- Top performing -----------------------------------------------------------------------

    [Fact]
    public async Task ATopPerformingRow_ShowsTestsLeadsSalesAndAConversionThatNeverExceedsOneHundred()
    {
        var tree = ACountry();
        RecordJourneys(tree.OutletOne, 1);
        Record(ASale(tree.OutletOne), ASale(tree.OutletOne), ASale(tree.OutletOne));

        var html = await DashboardAsync($"country={tree.CountryId}");

        // Four Sales against one Test: Sales ÷ Tests, the old figure, would have read 400%.
        Assert.Equal(new Entry(tree.OutletOneName, Tests: 1, Leads: 1, Sales: 4, Conversion: 100.0), Assert.Single(TopList(html, "Top outlets")));
        Assert.Equal(new Entry(tree.RetailerName, 1, 1, 4, 100.0), Assert.Single(TopList(html, "Top retailers")));
        Assert.Equal(new Entry(tree.CountryName, 1, 1, 4, 100.0), Assert.Single(TopList(html, "Top countries")));
    }

    [Fact]
    public async Task TheRankingSwitch_ReordersTheLists_AndKeepsTheOtherFilters()
    {
        var tree = ACountry();

        // Outlet One sells most but converts one Test in four; Outlet Two converts its only Test.
        // The direct outlet sells with no Tests at all.
        RecordJourneys(tree.OutletOne, 1);
        Record(ASale(tree.OutletOne), ASale(tree.OutletOne), ATest(tree.OutletOne), ATest(tree.OutletOne), ATest(tree.OutletOne));
        RecordJourneys(tree.OutletTwo, 1);
        Record(ASale(tree.DirectOutlet), ASale(tree.DirectOutlet));

        var bySales = await DashboardAsync($"country={tree.CountryId}&fromDate=2020-01-01");
        var byConversion = await DashboardAsync($"country={tree.CountryId}&fromDate=2020-01-01&rank=BestConversion");

        Assert.Equal(
            [tree.OutletOneName, tree.DirectOutletName, tree.OutletTwoName],
            TopList(bySales, "Top outlets").Select(e => e.Name));
        Assert.Equal(0.0, TopList(bySales, "Top outlets").Single(e => e.Name == tree.DirectOutletName).Conversion);

        // Ranked by conversion: best first, and the outlet with no Tests has none to rank.
        Assert.Equal(
            [tree.OutletTwoName, tree.OutletOneName],
            TopList(byConversion, "Top outlets").Select(e => e.Name));

        // Each switch link carries the country and the date range with its own ranking.
        var links = Regex.Match(bySales, @"id=""rankingSwitch"".*?</div>", RegexOptions.Singleline).Value;
        Assert.Matches($@"href=""/\?fromDate=2020-01-01&country={tree.CountryId}&rank=BestConversion""", links);
        Assert.Matches($@"href=""/\?fromDate=2020-01-01&country={tree.CountryId}&rank=MostSales""", links);

        // And applying the filter form keeps the chosen ranking.
        Assert.Contains("name=\"rank\" value=\"BestConversion\"", byConversion);
    }

    // --- Referrals logged ---------------------------------------------------------------------

    [Fact]
    public async Task ATestAndTheLeadItBecame_BothMarkedReferred_CountOnce()
    {
        var tree = ACountry();
        var test = ATest(tree.OutletOne, referred: true);
        Record(test, ALead(tree.OutletOne, test, referred: true));

        // And an unlinked referred Lead, which is a journey of its own.
        Record(ALead(tree.OutletTwo, referred: true));

        Assert.Equal(2, Tile(await DashboardAsync($"country={tree.CountryId}"), "Referrals logged"));
    }

    [Fact]
    public async Task TheReferralsTab_SaysItListsRecordsWhereTheTileCountsCustomers()
    {
        var html = WebUtility.HtmlDecode(await factory.CreateAdminClient().GetStringAsync("/EventHistory?tab=referrals"));

        Assert.Contains("counts that customer once", html);
    }
}

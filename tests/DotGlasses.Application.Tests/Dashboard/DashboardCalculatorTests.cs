using DotGlasses.Application.Dashboard;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Tests.Dashboard;

/// <summary>
/// The dashboard's definitions, each tested on rows held in memory: what a custom order and a
/// standard sale are (ADR-0008), what the Country and Retailer filters narrow, how a "Top
/// performing" row's conversion is worked out, and that a referral counts once per customer
/// journey. The database plays no part in any of them — DashboardQueryService only loads.
/// </summary>
public class DashboardCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganisationNodeSummary Dgi = Node("DOT Glasses International", OrganisationLevel.Dgi, "/1/");
    private static readonly OrganisationNodeSummary Kenya = Node("Kenya", OrganisationLevel.Country, "/1/2/");
    private static readonly OrganisationNodeSummary Kangemi = Node("Kangemi Vision", OrganisationLevel.Intermediate, "/1/2/3/");
    private static readonly OrganisationNodeSummary KangemiPost = Node("Kangemi Post", OrganisationLevel.RetailPoint, "/1/2/3/4/");
    private static readonly OrganisationNodeSummary KangemiStall = Node("Kangemi Stall", OrganisationLevel.RetailPoint, "/1/2/3/5/");
    private static readonly OrganisationNodeSummary NairobiDirect = Node("Nairobi Direct", OrganisationLevel.RetailPoint, "/1/2/6/");
    private static readonly OrganisationNodeSummary Uganda = Node("Uganda", OrganisationLevel.Country, "/1/7/");
    private static readonly OrganisationNodeSummary Kampala = Node("Kampala Optics", OrganisationLevel.Intermediate, "/1/7/8/");
    private static readonly OrganisationNodeSummary KampalaShop = Node("Kampala Shop", OrganisationLevel.RetailPoint, "/1/7/8/9/");
    private static readonly OrganisationNodeSummary TrainingRetailer = Node("Training School", OrganisationLevel.Intermediate, "/1/2/10/", isTraining: true);
    private static readonly OrganisationNodeSummary TrainingShop = Node("Training Shop", OrganisationLevel.RetailPoint, "/1/2/10/11/");

    private static readonly OrgTreeLookup Tree = new(
        [Dgi, Kenya, Kangemi, KangemiPost, KangemiStall, NairobiDirect, Uganda, Kampala, KampalaShop, TrainingRetailer, TrainingShop]);

    private static OrganisationNodeSummary Node(string name, OrganisationLevel level, string path, bool isTraining = false) =>
        new(Guid.NewGuid(), name, level, path, isTraining);

    /// <summary>The rows one test works with, and the calculation over them.</summary>
    private sealed class Rows
    {
        public List<Test> Tests { get; } = [];
        public List<Lead> Leads { get; } = [];
        public List<Sale> Sales { get; } = [];
        public List<CustomOrder> Orders { get; } = [];
        public Dictionary<Guid, string> Technicians { get; } = [];

        public Test Test(OrganisationNodeSummary at, DateTimeOffset? on = null, bool referred = false, Guid? technician = null)
        {
            var test = new Test { Id = Guid.NewGuid(), HierarchyPath = at.HierarchyPath, CreatedAtUtc = on ?? Now.AddDays(-1), ReferredOrTreated = referred, TechnicianUserId = technician ?? Guid.NewGuid() };
            Tests.Add(test);
            return test;
        }

        public Lead Lead(OrganisationNodeSummary at, Test? from = null, DateTimeOffset? on = null, bool referred = false, Guid? technician = null)
        {
            var lead = new Lead { Id = Guid.NewGuid(), HierarchyPath = at.HierarchyPath, CreatedAtUtc = on ?? Now.AddDays(-1), ReferredOrTreated = referred, TechnicianUserId = technician ?? Guid.NewGuid() };
            if (from is not null)
            {
                lead.SourceTestId = from.Id;
                from.ConvertedToLeadId = lead.Id;
            }

            Leads.Add(lead);
            return lead;
        }

        public Sale Sale(OrganisationNodeSummary at, Lead? from = null, DateTimeOffset? on = null, bool referred = false, Guid? technician = null)
        {
            var sale = new Sale { Id = Guid.NewGuid(), HierarchyPath = at.HierarchyPath, CreatedAtUtc = on ?? Now.AddDays(-1), ReferredOrTreated = referred, TechnicianUserId = technician ?? Guid.NewGuid() };
            if (from is not null)
            {
                sale.SourceLeadId = from.Id;
                from.SaleId = sale.Id;
                from.ConvertedFlag = true;
            }

            Sales.Add(sale);
            return sale;
        }

        /// <summary>A whole journey at one place: a Test that became a Lead that became a Sale.</summary>
        public Sale Journey(OrganisationNodeSummary at, Guid? technician = null) =>
            Sale(at, Lead(at, Test(at, technician: technician), technician: technician), technician: technician);

        public CustomOrder Order(OrganisationNodeSummary at, Lead? lead = null, Sale? sale = null, DateTimeOffset? placed = null)
        {
            var order = new CustomOrder { Id = Guid.NewGuid(), HierarchyPath = at.HierarchyPath, PlacedAtUtc = placed ?? Now.AddDays(-1), LeadId = lead?.Id, SaleId = sale?.Id };
            Orders.Add(order);
            return order;
        }

        public DashboardFigures Calculate(DashboardFilter? filter = null) =>
            DashboardCalculator.Calculate(Tests, Leads, Sales, Orders, Tree, Technicians, filter ?? new DashboardFilter(), Now);
    }

    // --- Custom orders and standard sales (ADR-0008) ------------------------------------------

    [Fact]
    public void ACustomOrderIsCountedWhetherOrNotAnyoneHasPaidForIt()
    {
        var rows = new Rows();
        rows.Order(KangemiPost, sale: rows.Sale(KangemiPost));
        rows.Order(KangemiPost, lead: rows.Lead(KangemiPost));

        Assert.Equal(2, rows.Calculate().CustomOrders);
    }

    [Fact]
    public void AStandardSaleIsASaleWithNoOrderBehindIt_SoTheTwoTilesNeverOverlap()
    {
        var rows = new Rows();
        rows.Sale(KangemiPost);
        rows.Order(KangemiPost, sale: rows.Sale(KangemiPost));

        // A Lead ordered its lens, then converted: the Sale shares the Lead's order.
        var orderingLead = rows.Lead(KangemiPost);
        rows.Order(KangemiPost, lead: orderingLead, sale: rows.Sale(KangemiPost, orderingLead));

        var figures = rows.Calculate();

        Assert.Equal(1, figures.StandardSales);
        Assert.Equal(2, figures.CustomOrders);
    }

    [Fact]
    public void ACustomOrderIsCountedByWhenItWasPlaced_NotWhenItWasPaidFor()
    {
        var rows = new Rows();
        var lead = rows.Lead(KangemiPost, on: Now.AddDays(-40));
        var sale = rows.Sale(KangemiPost, lead, on: Now.AddDays(-2));
        rows.Order(KangemiPost, lead, sale, placed: Now.AddDays(-40));

        var lastWeek = rows.Calculate(new DashboardFilter(FromUtc: Now.AddDays(-7)));
        var lastQuarter = rows.Calculate(new DashboardFilter(FromUtc: Now.AddDays(-90), ToUtcExclusive: Now.AddDays(-30)));

        Assert.Equal(0, lastWeek.CustomOrders);
        Assert.Equal(1, lastQuarter.CustomOrders);

        // The Sale made last week is not a standard sale either: its lens was ordered.
        Assert.Equal(0, lastWeek.StandardSales);
    }

    [Fact]
    public void TrainingOrganisationsAreLeftOutOfEveryFigure()
    {
        var rows = new Rows();
        rows.Journey(TrainingShop);
        rows.Order(TrainingShop, lead: rows.Lead(TrainingShop, referred: true));

        var figures = rows.Calculate();

        Assert.Equal(0, figures.TotalTests);
        Assert.Equal(0, figures.StandardSales);
        Assert.Equal(0, figures.CustomOrders);
        Assert.Equal(0, figures.PendingLeads);
        Assert.Equal(0, figures.ReferralsLogged);
        Assert.Empty(figures.TopOutlets);
    }

    // --- Country and Retailer filters ---------------------------------------------------------

    [Fact]
    public void ACountryFilterNarrowsEveryFigureToThatCountry()
    {
        var rows = new Rows();
        rows.Journey(KangemiPost);
        rows.Test(KangemiPost, referred: true);
        rows.Order(KangemiPost, lead: rows.Lead(NairobiDirect));
        rows.Journey(KampalaShop);
        rows.Journey(KampalaShop);

        var kenya = rows.Calculate(new DashboardFilter(CountryId: Kenya.Id));

        Assert.Equal(2, kenya.TotalTests);
        Assert.Equal(1, kenya.StandardSales);
        Assert.Equal(1, kenya.CustomOrders);
        Assert.Equal(1, kenya.PendingLeads);
        Assert.Equal(1, kenya.ReferralsLogged);
        Assert.Equal(50.0, kenya.TestToSaleConversionPercent);
        Assert.Equal("Kenya", Assert.Single(kenya.TopCountries).Name);
        Assert.DoesNotContain(kenya.TopOutlets, e => e.Name == "Kampala Shop");

        // The trend follows the organisation filter too: Kenya's one journey out of two Tests.
        Assert.Equal(50, kenya.ConversionTrendPercent[^1]);
    }

    [Fact]
    public void ARetailerFilterTakesEveryRetailPointBeneathThatRetailer()
    {
        var rows = new Rows();
        rows.Journey(KangemiPost);
        rows.Journey(KangemiStall);
        rows.Journey(NairobiDirect);
        rows.Journey(KampalaShop);

        var kangemi = rows.Calculate(new DashboardFilter(RetailerId: Kangemi.Id));

        Assert.Equal(2, kangemi.TotalTests);
        Assert.Equal(["Kangemi Post", "Kangemi Stall"], kangemi.TopOutlets.Select(e => e.Name).Order());
    }

    [Fact]
    public void NoRetailerSelectsTheRetailPointsThatHangDirectlyOffACountry()
    {
        var rows = new Rows();
        rows.Journey(KangemiPost);
        rows.Journey(NairobiDirect);

        var figures = rows.Calculate(new DashboardFilter(NoRetailer: true));

        Assert.Equal("Nairobi Direct", Assert.Single(figures.TopOutlets).Name);
    }

    [Fact]
    public void ACountryAndARetailerThatDoNotBelongTogether_ShowNothing()
    {
        var rows = new Rows();
        rows.Journey(KangemiPost);
        rows.Journey(KampalaShop);

        var figures = rows.Calculate(new DashboardFilter(CountryId: Uganda.Id, RetailerId: Kangemi.Id));

        Assert.Equal(0, figures.TotalTests);
        Assert.Equal(0, figures.StandardSales);
    }

    [Fact]
    public void AnOrganisationThatIsNotInTheTree_ShowsNothingRatherThanEverything()
    {
        var rows = new Rows();
        rows.Journey(KangemiPost);

        Assert.Equal(0, rows.Calculate(new DashboardFilter(CountryId: Guid.NewGuid())).TotalTests);
        Assert.Equal(0, rows.Calculate(new DashboardFilter(RetailerId: Guid.NewGuid())).TotalTests);
        Assert.Equal(0, rows.Calculate(new DashboardFilter(RetailerId: Guid.Empty)).TotalTests);
    }

    // --- Top performing -----------------------------------------------------------------------

    [Fact]
    public void ATopPerformingRowShowsTestsLeadsSalesAndConversion()
    {
        var rows = new Rows();
        rows.Journey(KangemiPost);
        rows.Lead(KangemiPost, rows.Test(KangemiPost));
        rows.Test(KangemiPost);
        rows.Test(KangemiPost);

        var entry = Assert.Single(rows.Calculate().TopOutlets);

        Assert.Equal(new DashboardRankedEntry("Kangemi Post", Tests: 4, Leads: 2, Sales: 1, ConversionPercent: 25.0), entry);
    }

    [Fact]
    public void ConversionNeverExceedsOneHundredPercent_EvenWithMoreSalesThanTests()
    {
        var rows = new Rows();
        rows.Journey(KangemiPost);
        rows.Sale(KangemiPost);
        rows.Sale(KangemiPost);
        rows.Sale(KangemiPost);

        var entry = Assert.Single(rows.Calculate().TopOutlets);

        Assert.Equal(1, entry.Tests);
        Assert.Equal(4, entry.Sales);
        Assert.Equal(100.0, entry.ConversionPercent);
    }

    [Fact]
    public void SalesWithNoTests_ShowZeroPercentAndAreLeftOutWhenRankingByConversion()
    {
        var rows = new Rows();
        rows.Sale(KangemiPost);
        rows.Sale(KangemiPost);
        rows.Journey(KangemiStall);

        var bySales = rows.Calculate(new DashboardFilter(Ranking: DashboardRanking.MostSales)).TopOutlets;
        var byConversion = rows.Calculate(new DashboardFilter(Ranking: DashboardRanking.BestConversion)).TopOutlets;

        Assert.Equal(new DashboardRankedEntry("Kangemi Post", Tests: 0, Leads: 0, Sales: 2, ConversionPercent: 0), bySales[0]);
        Assert.Equal("Kangemi Stall", Assert.Single(byConversion).Name);
    }

    [Fact]
    public void TheRankingSwitchReordersAListWhereTheTwoRankingsDiffer()
    {
        var rows = new Rows();

        // The Post sells most but converts one Test in four; the Stall converts its only Test.
        rows.Journey(KangemiPost);
        rows.Sale(KangemiPost);
        rows.Sale(KangemiPost);
        rows.Test(KangemiPost);
        rows.Test(KangemiPost);
        rows.Test(KangemiPost);
        rows.Journey(KangemiStall);

        var bySales = rows.Calculate(new DashboardFilter(Ranking: DashboardRanking.MostSales)).TopOutlets;
        var byConversion = rows.Calculate(new DashboardFilter(Ranking: DashboardRanking.BestConversion)).TopOutlets;

        Assert.Equal(["Kangemi Post", "Kangemi Stall"], bySales.Select(e => e.Name));
        Assert.Equal(["Kangemi Stall", "Kangemi Post"], byConversion.Select(e => e.Name));
    }

    [Fact]
    public void WhetherATestConvertedIsJudgedOnItsWholeJourney_TheDateRangeOnlyChoosesTheTests()
    {
        var rows = new Rows();
        var test = rows.Test(KangemiPost, on: Now.AddDays(-20));
        rows.Sale(KangemiPost, rows.Lead(KangemiPost, test, on: Now.AddDays(-10)), on: Now.AddDays(-1));

        // The range holds the Test but neither the Lead nor the Sale it became.
        var figures = rows.Calculate(new DashboardFilter(FromUtc: Now.AddDays(-25), ToUtcExclusive: Now.AddDays(-15)));

        Assert.Equal(new DashboardRankedEntry("Kangemi Post", Tests: 1, Leads: 0, Sales: 0, ConversionPercent: 100.0), Assert.Single(figures.TopOutlets));
        Assert.Equal(100.0, figures.TestToSaleConversionPercent);
    }

    [Fact]
    public void TheListsStayTopFive_AndARowThatCannotBeAttributedIsLeftOutOfRetailersAndCountries()
    {
        var rows = new Rows();
        var technicians = Enumerable.Range(1, 7).Select(i => Guid.NewGuid()).ToList();
        for (var i = 0; i < technicians.Count; i++)
        {
            rows.Technicians[technicians[i]] = $"Technician {i + 1}";
            rows.Journey(KangemiPost, technicians[i]);
        }

        rows.Journey(NairobiDirect);
        rows.Journey(Dgi);

        var figures = rows.Calculate();

        Assert.Equal(5, figures.TopTechnicians.Count);
        Assert.Equal("Kangemi Vision", Assert.Single(figures.TopRetailers).Name);
        Assert.Equal("Kenya", Assert.Single(figures.TopCountries).Name);
        Assert.Equal(8, figures.TopCountries[0].Sales);
    }

    // --- Referrals logged ---------------------------------------------------------------------

    [Fact]
    public void AReferredTestContinuedIntoAReferredLead_CountsOnce()
    {
        var rows = new Rows();
        rows.Lead(KangemiPost, rows.Test(KangemiPost, referred: true), referred: true);

        Assert.Equal(1, rows.Calculate().ReferralsLogged);
    }

    [Fact]
    public void WithTheSaleItBecameAlsoReferred_StillOnce()
    {
        var rows = new Rows();
        rows.Sale(KangemiPost, rows.Lead(KangemiPost, rows.Test(KangemiPost, referred: true), referred: true), referred: true);

        Assert.Equal(1, rows.Calculate().ReferralsLogged);
    }

    [Fact]
    public void TwoUnlinkedReferredRecords_CountTwo()
    {
        var rows = new Rows();
        rows.Test(KangemiPost, referred: true);
        rows.Lead(KangemiPost, referred: true);
        rows.Sale(KangemiPost);

        Assert.Equal(2, rows.Calculate().ReferralsLogged);
    }

    [Fact]
    public void AJourneyWithOnlyItsLeadReferred_CountsOnce()
    {
        var rows = new Rows();
        rows.Sale(KangemiPost, rows.Lead(KangemiPost, rows.Test(KangemiPost), referred: true));

        Assert.Equal(1, rows.Calculate().ReferralsLogged);
    }

    [Fact]
    public void AJourneyWithNoReferredRecord_CountsNothing()
    {
        var rows = new Rows();
        rows.Journey(KangemiPost);

        Assert.Equal(0, rows.Calculate().ReferralsLogged);
    }

    [Fact]
    public void AJourneyCountsWhenAnyOfItsReferredRecordsFallsInTheRange_AndOnceWhenSeveralDo()
    {
        var rows = new Rows();
        var test = rows.Test(KangemiPost, on: Now.AddDays(-20), referred: true);
        rows.Sale(KangemiPost, rows.Lead(KangemiPost, test, on: Now.AddDays(-10), referred: true), on: Now.AddDays(-1));

        Assert.Equal(1, rows.Calculate(new DashboardFilter(FromUtc: Now.AddDays(-25), ToUtcExclusive: Now.AddDays(-15))).ReferralsLogged);
        Assert.Equal(1, rows.Calculate(new DashboardFilter(FromUtc: Now.AddDays(-12), ToUtcExclusive: Now.AddDays(-5))).ReferralsLogged);
        Assert.Equal(1, rows.Calculate(new DashboardFilter(FromUtc: Now.AddDays(-30))).ReferralsLogged);

        // Only the unreferred Sale falls in the last week.
        Assert.Equal(0, rows.Calculate(new DashboardFilter(FromUtc: Now.AddDays(-5))).ReferralsLogged);
    }
}

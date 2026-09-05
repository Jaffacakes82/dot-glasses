using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Event History's four tabs, each of which now answers both the screen and the CSV export from
/// one method — a <see cref="PageRequest"/> for a page of rows, null for all of them.
///
/// These need Postgres rather than the fakes the Application tests use, because what they pin is
/// what SQL does with the filters: the Leads tab's name search is an ILIKE (a plain Contains
/// translates to a case-sensitive LIKE against these non-citext columns), it runs as a subquery
/// so that it filters before the page is taken, the Referrals tab is a UNION ALL across three
/// tables, and the hierarchy scoping every one of them inherits is a SQL string prefix match on
/// the global query filter. None of that is real under an in-memory provider evaluating the same
/// expressions in C#.
///
/// The export half is asserted as an equality against the paged half rather than re-asserted row
/// by row — the point of the collapse is that there is nothing left that could differ.
/// </summary>
[Collection(PostgresCollection.Name)]
public class EventHistoryQueryTests(PostgresContainerFixture postgres)
{
    /// <summary>A second retail point beside the seeded tree's Outreach Post, under the same
    /// Kenyan retailer. Segment 5 continues the seed's run.</summary>
    private const string SecondOutletPath = "/1/2/3/5/";

    private static readonly DateTimeOffset Day1 = At(1);
    private static readonly DateTimeOffset Day2 = At(2);
    private static readonly DateTimeOffset Day3 = At(3);

    private static readonly Guid ReferralReasonId = new("c0000000-0000-0000-0000-0000000000f1");

    /// <summary>Mid-morning on the given March day — far enough inside the day that a filter
    /// built from midnight boundaries either includes the whole day or none of it.</summary>
    private static DateTimeOffset At(int day) => new(2026, 3, day, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Midnight on the given March day, i.e. what DateRange.ToUtcRange hands the query
    /// service for a picked date — inclusive as a "from", exclusive as a "to".</summary>
    private static DateTimeOffset Midnight(int day) => new(2026, 3, day, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SalesAreNewestFirstWithinAHalfOpenDateRange()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context => context.Sales.AddRange(
            NewSale(Day1), NewSale(Day2), NewSale(Day3)));

        await using var context = CreateContext(connectionString);

        var all = await CreateService(context).ListSalesAsync(null, null, Paged());
        Assert.Equal(new[] { Day3, Day2, Day1 }, all.Rows.Select(r => r.CreatedAtUtc));

        // Day 2's midnight includes the whole of day 2; day 3's excludes the whole of day 3.
        var middleDay = await CreateService(context).ListSalesAsync(Midnight(2), Midnight(3), Paged());
        Assert.Equal(Day2, Assert.Single(middleDay.Rows).CreatedAtUtc);
    }

    [Fact]
    public async Task TestsAreNewestFirstWithinAHalfOpenDateRange()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context => context.Tests.AddRange(
            NewTest(Day1), NewTest(Day2), NewTest(Day3)));

        await using var context = CreateContext(connectionString);

        var all = await CreateService(context).ListTestsAsync(null, null, Paged());
        Assert.Equal(new[] { Day3, Day2, Day1 }, all.Rows.Select(r => r.CreatedAtUtc));
        Assert.All(all.Rows, r => Assert.Null(r.Name));

        var fromDayTwo = await CreateService(context).ListTestsAsync(Midnight(2), null, Paged());
        Assert.Equal(new[] { Day3, Day2 }, fromDayTwo.Rows.Select(r => r.CreatedAtUtc));
    }

    /// <summary>The reason ILIKE is there rather than Contains: the stored name is capitalised and
    /// the admin typing into the search box is not, and neither the column nor the database
    /// collation is case-insensitive.</summary>
    [Fact]
    public async Task ALeadNameSearchMatchesRegardlessOfCase()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var jane = Guid.NewGuid();
        var john = Guid.NewGuid();
        await SeedAsync(connectionString, context =>
        {
            context.Customers.AddRange(NewCustomer(jane, "Jane Doe"), NewCustomer(john, "JOHN SMITH"));
            context.Leads.AddRange(NewLead(Day1, customerId: jane), NewLead(Day2, customerId: john));
        });

        await using var context = CreateContext(connectionString);

        var lowercase = await CreateService(context).ListLeadsAsync("jane", null, null, Paged());
        Assert.Equal("Jane Doe", Assert.Single(lowercase.Rows).Name);

        var mixedCase = await CreateService(context).ListLeadsAsync("John", null, null, Paged());
        Assert.Equal("JOHN SMITH", Assert.Single(mixedCase.Rows).Name);

        var partial = await CreateService(context).ListLeadsAsync("o", null, null, Paged());
        Assert.Equal(2, partial.TotalCount);
    }

    /// <summary>If the search ran after the page was taken, page 1 would come back short and the
    /// count would be everyone's — so "page 2" would mean something different depending on how
    /// many rows the search happened to remove from page 1.</summary>
    [Fact]
    public async Task ALeadNameSearchNarrowsTheResultBeforeItIsPaged()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context =>
        {
            for (var i = 0; i < 3; i++)
            {
                var matching = Guid.NewGuid();
                var other = Guid.NewGuid();
                context.Customers.AddRange(NewCustomer(matching, $"Amina Otieno {i}"), NewCustomer(other, $"Brian Mwangi {i}"));
                context.Leads.AddRange(NewLead(At(10 + i), customerId: matching), NewLead(At(20 + i), customerId: other));
            }
        });

        await using var context = CreateContext(connectionString);

        var firstPage = await CreateService(context).ListLeadsAsync("amina", null, null, Paged(page: 1, pageSize: 2));
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Rows.Count);
        Assert.All(firstPage.Rows, r => Assert.StartsWith("Amina", r.Name));

        var secondPage = await CreateService(context).ListLeadsAsync("amina", null, null, Paged(page: 2, pageSize: 2));
        Assert.Equal(3, secondPage.TotalCount);
        Assert.StartsWith("Amina", Assert.Single(secondPage.Rows).Name);
    }

    [Fact]
    public async Task ReferralsMergeTestLeadAndSaleAndCarryOnlyReferredRows()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context =>
        {
            context.ReferenceDataItems.Add(NewReferralReason("Cataract suspected"));
            context.Tests.AddRange(NewTest(Day1, referredOrTreated: true), NewTest(Day1));
            context.Leads.AddRange(NewLead(Day2, referredOrTreated: true), NewLead(Day2));
            context.Sales.AddRange(NewSale(Day3, referredOrTreated: true), NewSale(Day3));
        });

        await using var context = CreateContext(connectionString);
        var referrals = await CreateService(context).ListReferralsAsync(null, null, Paged());

        Assert.Equal(3, referrals.TotalCount);
        Assert.Equal(new[] { "Sale", "Lead", "Test" }, referrals.Rows.Select(r => r.Source));
        Assert.All(referrals.Rows, r => Assert.Equal("Cataract suspected", r.Reason));
    }

    [Fact]
    public async Task ReferralsAreFilteredByDateAcrossAllThreeSources()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context =>
        {
            context.ReferenceDataItems.Add(NewReferralReason("Cataract suspected"));
            context.Tests.Add(NewTest(Day1, referredOrTreated: true));
            context.Leads.Add(NewLead(Day2, referredOrTreated: true));
            context.Sales.Add(NewSale(Day3, referredOrTreated: true));
        });

        await using var context = CreateContext(connectionString);
        var referrals = await CreateService(context).ListReferralsAsync(Midnight(2), Midnight(3), Paged());

        Assert.Equal("Lead", Assert.Single(referrals.Rows).Source);
    }

    /// <summary>The whole point of the collapse: an export is the same call with paging omitted,
    /// so it can only ever be the pages laid end to end.</summary>
    [Fact]
    public async Task AnUnpagedTabReturnsExactlyWhatItsPagesDo()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context => context.Sales.AddRange(
            NewSale(At(1)), NewSale(At(2)), NewSale(At(3)), NewSale(At(4)), NewSale(At(5))));

        await using var context = CreateContext(connectionString);

        var unpaged = await CreateService(context).ListSalesAsync(null, null, paging: null);
        var firstPage = await CreateService(context).ListSalesAsync(null, null, Paged(page: 1, pageSize: 3));
        var secondPage = await CreateService(context).ListSalesAsync(null, null, Paged(page: 2, pageSize: 3));

        Assert.Equal(5, unpaged.TotalCount);
        Assert.Equal(unpaged.TotalCount, firstPage.TotalCount);
        Assert.Equal(unpaged.Rows, firstPage.Rows.Concat(secondPage.Rows));
    }

    /// <summary>The same filter reaches the export, so a search the admin typed cannot quietly
    /// widen when the CSV is generated.</summary>
    [Fact]
    public async Task AnUnpagedLeadsTabKeepsTheSearchAndDateFiltersTheScreenApplied()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var amina = Guid.NewGuid();
        var brian = Guid.NewGuid();
        await SeedAsync(connectionString, context =>
        {
            context.Customers.AddRange(NewCustomer(amina, "Amina Otieno"), NewCustomer(brian, "Brian Mwangi"));
            context.Leads.AddRange(
                NewLead(Day1, customerId: amina),
                NewLead(Day2, customerId: amina),
                NewLead(Day2, customerId: brian));
        });

        await using var context = CreateContext(connectionString);

        var onScreen = await CreateService(context).ListLeadsAsync("amina", Midnight(2), Midnight(3), Paged());
        var exported = await CreateService(context).ListLeadsAsync("amina", Midnight(2), Midnight(3), paging: null);

        Assert.Equal("Amina Otieno", Assert.Single(onScreen.Rows).Name);
        Assert.Equal(onScreen.Rows, exported.Rows);
    }

    /// <summary>Unpaged means "every row this caller can see", never "every row" — the export
    /// inherits the hierarchy filter because it is the same query, not a second one that also
    /// remembered to apply it.</summary>
    [Fact]
    public async Task AnUnpagedTabStillCannotReachRowsOutsideTheCallersScope()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context =>
        {
            context.OrganisationNodes.Add(NewSecondOutlet());
            context.Sales.AddRange(
                NewSale(Day1, OrganisationSeedConfiguration.KenyaRetailPointPath),
                NewSale(Day2, SecondOutletPath));
        });

        await using var wholeTree = CreateContext(connectionString);
        var everything = await CreateService(wholeTree).ListSalesAsync(null, null, paging: null);
        Assert.Equal(2, everything.TotalCount);

        // Scoped at one outlet: its neighbour's sale is not merely off the first page, it is
        // outside this caller's subtree and so outside the query altogether.
        await using var oneOutlet = CreateContext(connectionString, OrganisationSeedConfiguration.KenyaRetailPointPath);
        var mine = await CreateService(oneOutlet).ListSalesAsync(null, null, paging: null);

        var row = Assert.Single(mine.Rows);
        Assert.Equal(Day1, row.CreatedAtUtc);
        Assert.Equal("Kangemi Vision Centre — Outreach Post", row.Outlet);

        // Resolved through IUnscopedReportQueryService — a caller scoped at the outlet can never
        // see their own Country node through the hierarchy filter (CLAUDE.md's standing gotcha).
        Assert.Equal("Kenya", row.Country);
    }

    private static DotGlassesDbContext CreateContext(string connectionString, string hierarchyPathPrefix = OrganisationSeedConfiguration.DgiPath) =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, hierarchyPathPrefix));

    private static EventHistoryQueryService CreateService(DotGlassesDbContext context) =>
        new(context, new ReferenceDataSnapshotProvider(context), new UnscopedReportQueryService(context));

    private static PageRequest Paged(int page = 1, int pageSize = 25) => new(page, pageSize);

    private static async Task SeedAsync(string connectionString, Action<DotGlassesDbContext> seed)
    {
        await using var context = CreateContext(connectionString);
        seed(context);
        await context.SaveChangesAsync();
    }

    private static OrganisationNode NewSecondOutlet() => new()
    {
        Id = Guid.NewGuid(),
        ParentId = OrganisationSeedConfiguration.KenyaRetailerId,
        Name = "Kangemi Vision Centre — Market Stall",
        Level = OrganisationLevel.RetailPoint,
        HierarchyPath = SecondOutletPath,
    };

    private static Sale NewSale(DateTimeOffset createdAtUtc, string hierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, bool referredOrTreated = false) => new()
    {
        Id = Guid.NewGuid(),
        HierarchyPath = hierarchyPath,
        TechnicianUserId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        FulfilmentStatus = FulfilmentStatus.Submitted,
        ReferredOrTreated = referredOrTreated,
        ReferralReasonRefId = referredOrTreated ? ReferralReasonId : null,
        CreatedAtUtc = createdAtUtc,
    };

    private static Test NewTest(DateTimeOffset createdAtUtc, string hierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, bool referredOrTreated = false) => new()
    {
        Id = Guid.NewGuid(),
        HierarchyPath = hierarchyPath,
        TechnicianUserId = Guid.NewGuid(),
        ReferredOrTreated = referredOrTreated,
        ReferralReasonRefId = referredOrTreated ? ReferralReasonId : null,
        CreatedAtUtc = createdAtUtc,
    };

    private static Lead NewLead(DateTimeOffset createdAtUtc, string hierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, Guid? customerId = null, bool referredOrTreated = false) => new()
    {
        Id = Guid.NewGuid(),
        HierarchyPath = hierarchyPath,
        TechnicianUserId = Guid.NewGuid(),
        CustomerId = customerId ?? Guid.NewGuid(),
        ReasonNotPurchasedRefId = Guid.NewGuid(),
        ReferredOrTreated = referredOrTreated,
        ReferralReasonRefId = referredOrTreated ? ReferralReasonId : null,
        CreatedAtUtc = createdAtUtc,
    };

    private static Customer NewCustomer(Guid id, string fullName) => new()
    {
        Id = id,
        HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath,
        FullName = fullName,
        PhoneNumber = "+254700123456",
    };

    private static ReferenceDataItem NewReferralReason(string label) => new()
    {
        Id = ReferralReasonId,
        Category = ReferenceDataCategory.ReferralReason,
        Code = "cataract",
        Label = label,
    };
}

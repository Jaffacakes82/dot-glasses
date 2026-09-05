using DotGlasses.Application.Leads;
using DotGlasses.Application.Sales;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// The two conversions (Test -> Lead, Lead -> Sale) driven by their real services over the real
/// repositories and a real database, asserting the all-or-nothing guarantee end to end.
///
/// These belong here rather than beside the Application tests for two reasons. The scoping miss
/// the refusal turns on is a SQL prefix match on the global query filter, not something a
/// dictionary-backed fake can produce for real; and the atomicity half needs an actual
/// transaction, which the EF Core InMemory provider does not implement at all — under it a
/// half-written batch would have "passed" silently, which is precisely the class of false
/// confidence this suite exists to remove (see TransactionBehaviourTests).
///
/// The Application-level tests still own the refusal's *shape* (no writes attempted, no commit);
/// what is added here is that the database agrees.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ConversionAtomicityTests(PostgresContainerFixture postgres)
{
    private const string CallerOutlet = OrganisationSeedConfiguration.KenyaRetailPointPath;

    /// <summary>A sibling outlet under the same retailer — beside the caller, never beneath
    /// them, so the hierarchy filter hides its rows. Segment 5 continues the seed's run.</summary>
    private const string SiblingOutlet = "/1/2/3/5/";

    [Fact]
    public async Task ConvertingAVisibleTest_LandsTheLeadAndTheBackLinkInTheDatabaseTogether()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var testId = Guid.NewGuid();
        await SeedAsync(connectionString, context => context.Tests.Add(NewTest(testId, CallerOutlet)));

        Guid leadId;
        await using (var context = CreateContext(connectionString))
        {
            var lead = await LeadServiceOver(context).CreateAsync(
                ARecordedLead(sourceTestId: testId), Guid.NewGuid(), CallerOutlet);
            leadId = lead.Id;
        }

        // Read back on a fresh connection: both halves are actually committed, not merely tracked.
        await using var verifyContext = CreateContext(connectionString);
        Assert.NotNull(await verifyContext.Leads.FirstOrDefaultAsync(l => l.Id == leadId));
        Assert.Equal(leadId, (await verifyContext.Tests.FirstAsync(t => t.Id == testId)).ConvertedToLeadId);
    }

    [Fact]
    public async Task ConvertingATestAtASiblingOutlet_IsRefusedAndLeavesTheDatabaseUntouched()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var testId = Guid.NewGuid();
        await SeedAsync(connectionString, context => context.Tests.Add(NewTest(testId, SiblingOutlet)), SiblingOutlet);

        await using (var context = CreateContext(connectionString))
        {
            await Assert.ThrowsAsync<DomainRuleViolationException>(() => LeadServiceOver(context).CreateAsync(
                ARecordedLead(sourceTestId: testId), Guid.NewGuid(), CallerOutlet));
        }

        // IgnoreQueryFilters throughout: the assertion is "nothing was written anywhere", which a
        // scoped read could satisfy simply by not being able to see what was.
        await using var verifyContext = CreateContext(connectionString);
        Assert.Empty(await verifyContext.Leads.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await verifyContext.Customers.IgnoreQueryFilters().ToListAsync());
        Assert.Null((await verifyContext.Tests.IgnoreQueryFilters().FirstAsync(t => t.Id == testId)).ConvertedToLeadId);
    }

    [Fact]
    public async Task ALeadInsertRejectedByTheDatabase_DiscardsTheSourceTestsBackLinkWithIt()
    {
        // The atomicity claim itself, forced at the database rather than argued from the single
        // SaveChangesAsync call. The caller's claim still points at their own outlet, so the
        // source Test resolves and the back-link is staged — but the path stamped onto the new
        // rows overflows HierarchyPath's varchar(1000), so the INSERT fails. The back-link UPDATE
        // rides in the same batch and must go down with it: a Test must never be left marked
        // converted to a Lead that does not exist.
        var connectionString = await postgres.CreateDatabaseAsync();
        var testId = Guid.NewGuid();
        await SeedAsync(connectionString, context => context.Tests.Add(NewTest(testId, CallerOutlet)));

        await using (var context = CreateContext(connectionString))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => LeadServiceOver(context).CreateAsync(
                ARecordedLead(sourceTestId: testId), Guid.NewGuid(), TooLongForTheColumn()));
        }

        await using var verifyContext = CreateContext(connectionString);
        Assert.Empty(await verifyContext.Leads.IgnoreQueryFilters().ToListAsync());
        Assert.Null((await verifyContext.Tests.IgnoreQueryFilters().FirstAsync(t => t.Id == testId)).ConvertedToLeadId);
    }

    [Fact]
    public async Task ConvertingAVisibleLead_LandsTheSaleAndTheBackLinkInTheDatabaseTogether()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var leadId = Guid.NewGuid();
        await SeedAsync(connectionString, context => context.Leads.Add(NewLead(leadId, CallerOutlet)));

        Guid saleId;
        await using (var context = CreateContext(connectionString))
        {
            var sale = await SaleServiceOver(context).CreateAsync(
                ARecordedSale(sourceLeadId: leadId), Guid.NewGuid(), CallerOutlet);
            saleId = sale.Id;
        }

        await using var verifyContext = CreateContext(connectionString);
        Assert.NotNull(await verifyContext.Sales.FirstOrDefaultAsync(s => s.Id == saleId));
        Assert.Single(await verifyContext.SaleCoatings.Where(c => c.SaleId == saleId).ToListAsync());

        var sourceLead = await verifyContext.Leads.FirstAsync(l => l.Id == leadId);
        Assert.True(sourceLead.ConvertedFlag);
        Assert.Equal(saleId, sourceLead.SaleId);
    }

    [Fact]
    public async Task ConvertingALeadAtASiblingOutlet_IsRefusedAndLeavesTheDatabaseUntouched()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var leadId = Guid.NewGuid();
        await SeedAsync(connectionString, context => context.Leads.Add(NewLead(leadId, SiblingOutlet)), SiblingOutlet);

        await using (var context = CreateContext(connectionString))
        {
            await Assert.ThrowsAsync<DomainRuleViolationException>(() => SaleServiceOver(context).CreateAsync(
                ARecordedSale(sourceLeadId: leadId), Guid.NewGuid(), CallerOutlet));
        }

        await using var verifyContext = CreateContext(connectionString);
        Assert.Empty(await verifyContext.Sales.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await verifyContext.SaleCoatings.ToListAsync());
        Assert.Empty(await verifyContext.Customers.IgnoreQueryFilters().ToListAsync());

        var sourceLead = await verifyContext.Leads.IgnoreQueryFilters().FirstAsync(l => l.Id == leadId);
        Assert.False(sourceLead.ConvertedFlag);
        Assert.Null(sourceLead.SaleId);
    }

    [Fact]
    public async Task ASaleInsertRejectedByTheDatabase_DiscardsTheSourceLeadsBackLinkAndItsCoatings()
    {
        // The Sale-side twin of the Lead case above — see its comment for why the path overflows.
        var connectionString = await postgres.CreateDatabaseAsync();
        var leadId = Guid.NewGuid();
        await SeedAsync(connectionString, context => context.Leads.Add(NewLead(leadId, CallerOutlet)));

        await using (var context = CreateContext(connectionString))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => SaleServiceOver(context).CreateAsync(
                ARecordedSale(sourceLeadId: leadId), Guid.NewGuid(), TooLongForTheColumn()));
        }

        await using var verifyContext = CreateContext(connectionString);
        Assert.Empty(await verifyContext.Sales.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await verifyContext.SaleCoatings.ToListAsync());

        var sourceLead = await verifyContext.Leads.IgnoreQueryFilters().FirstAsync(l => l.Id == leadId);
        Assert.False(sourceLead.ConvertedFlag);
        Assert.Null(sourceLead.SaleId);
    }

    /// <summary>Longer than HierarchyPath's varchar(1000), so any row stamped with it is rejected
    /// by Postgres on INSERT — the trigger for the two rollback tests.</summary>
    private static string TooLongForTheColumn() => $"/{new string('9', 1001)}/";

    private static DotGlassesDbContext CreateContext(string connectionString, string hierarchyPathPrefix = CallerOutlet) =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, hierarchyPathPrefix));

    private static LeadService LeadServiceOver(DotGlassesDbContext context) =>
        new(new LeadRepository(context), new TestRepository(context), new CustomerRepository(context), context);

    private static SaleService SaleServiceOver(DotGlassesDbContext context) =>
        new(new SaleRepository(context), new LeadRepository(context), new CustomerRepository(context), context);

    /// <summary>Seeds through a context scoped at the row's own path, so the global filter doesn't
    /// hide what is being written.</summary>
    private static async Task SeedAsync(string connectionString, Action<DotGlassesDbContext> seed, string seederPath = CallerOutlet)
    {
        await using var context = CreateContext(connectionString, seederPath);
        seed(context);
        await context.SaveChangesAsync();
    }

    private static Test NewTest(Guid id, string hierarchyPath) => new()
    {
        Id = id,
        HierarchyPath = hierarchyPath,
        TechnicianUserId = Guid.NewGuid(),
    };

    private static Lead NewLead(Guid id, string hierarchyPath) => new()
    {
        Id = id,
        HierarchyPath = hierarchyPath,
        TechnicianUserId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        ConvertedFlag = false,
    };

    private static CreateLeadRequest ARecordedLead(Guid? sourceTestId = null) => new()
    {
        Id = Guid.NewGuid(),
        SourceTestId = sourceTestId,
        FullName = "Amina Okoro",
        PhoneNumber = "0700111222",
        AgeYears = 42,
        Gender = Gender.Female,
        ConsentGiven = true,
        ReferredOrTreated = false,
        ReasonNotPurchasedRefId = Guid.NewGuid(),
        LensRangeType = LensRangeType.SixLensSet,
    };

    private static CreateSaleRequest ARecordedSale(Guid? sourceLeadId = null) => new()
    {
        Id = Guid.NewGuid(),
        SourceLeadId = sourceLeadId,
        FullName = "Amina Okoro",
        PhoneNumber = "0700111222",
        AgeYears = 42,
        Gender = Gender.Female,
        ConsentGiven = true,
        ReferredOrTreated = false,
        LensRangeType = LensRangeType.SixLensSet,
        OrderFromDotGlasses = false,
        FrameColourRefId = Guid.NewGuid(),
        FrameCoverage = ContractFrameCoverage.FullFrame,
        CoatingRefIds = [Guid.NewGuid()],
    };
}

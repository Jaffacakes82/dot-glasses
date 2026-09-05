using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Advancing a custom order is the one write action on a screen several admins share, so every
/// way it can be refused has to arrive as a sentence an admin can read — CustomOrdersController
/// surfaces the exception message verbatim in the page's validation summary. The
/// already-Fulfilled case is the live one (a colleague got there first, a double click, a browser
/// resubmit); the out-of-scope case matters because the sale is hidden by the hierarchy query
/// filter rather than absent, so a naive FirstAsync would surface EF's own "sequence contains no
/// elements" instead.
///
/// The out-of-scope case is also why this class runs against real Postgres rather than the
/// in-memory provider it was first written for: it turns entirely on the query filter's prefix
/// match, which the in-memory provider evaluated in C# and Postgres evaluates in SQL. Testing it
/// where the application actually runs it is the point of the harness.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CustomOrderAdvanceStatusTests(PostgresContainerFixture postgres)
{
    private static DotGlassesDbContext CreateContext(string connectionString, string hierarchyPathPrefix = "") =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, hierarchyPathPrefix));

    private static async Task<Guid> SeedSaleAsync(string connectionString, FulfilmentStatus? status, string hierarchyPath = "/1/4/")
    {
        var saleId = Guid.NewGuid();
        await using var seedContext = CreateContext(connectionString);

        seedContext.Sales.Add(new Sale
        {
            Id = saleId,
            HierarchyPath = hierarchyPath,
            TechnicianUserId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            FulfilmentStatus = status,
        });

        await seedContext.SaveChangesAsync();
        return saleId;
    }

    [Fact]
    public async Task AdvancingAnActiveOrder_MovesItToTheNextStatus()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var saleId = await SeedSaleAsync(connectionString, FulfilmentStatus.Submitted);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: "/1/");
        await new CustomOrderService(context, new UnscopedReportQueryService(context)).AdvanceStatusAsync(saleId);

        Assert.Equal(FulfilmentStatus.InLab, (await context.Sales.SingleAsync(x => x.Id == saleId)).FulfilmentStatus);
    }

    [Fact]
    public async Task AdvancingAnAlreadyFulfilledOrder_ExplainsItselfRatherThanFailingRaw()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var saleId = await SeedSaleAsync(connectionString, FulfilmentStatus.Fulfilled);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: "/1/");

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => new CustomOrderService(context, new UnscopedReportQueryService(context)).AdvanceStatusAsync(saleId));
        Assert.Equal("This custom order is already Fulfilled.", ex.Message);
    }

    [Fact]
    public async Task AdvancingAnOrderOutsideTheCallersScope_ExplainsItselfRatherThanFailingRaw()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var saleId = await SeedSaleAsync(connectionString, FulfilmentStatus.Submitted, hierarchyPath: "/1/40/");

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: "/1/4/");

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => new CustomOrderService(context, new UnscopedReportQueryService(context)).AdvanceStatusAsync(saleId));
        Assert.Equal("This custom order is no longer available.", ex.Message);
    }

    [Fact]
    public async Task AdvancingASaleThatIsNotACustomOrder_ExplainsItselfRatherThanFailingRaw()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var saleId = await SeedSaleAsync(connectionString, status: null);

        await using var context = CreateContext(connectionString, hierarchyPathPrefix: "/1/");

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => new CustomOrderService(context, new UnscopedReportQueryService(context)).AdvanceStatusAsync(saleId));
        Assert.Equal("This Sale is not a custom order routed to fulfilment.", ex.Message);
    }
}

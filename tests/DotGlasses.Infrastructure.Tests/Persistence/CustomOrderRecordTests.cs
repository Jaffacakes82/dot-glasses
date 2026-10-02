using DotGlasses.Application.Leads;
using DotGlasses.Application.Sales;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;
using FulfilmentStatus = DotGlasses.Domain.Enums.FulfilmentStatus;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// The custom order as a record of its own (ADR-0008), against the real database: the migration
/// that lifted it off the Sale, that an order and the Lead or Sale placing it are written together
/// or not at all, that a record sent twice places one order, and that the queue reads an order's
/// lens through whichever record placed it.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CustomOrderRecordTests(PostgresContainerFixture postgres)
{
    private const string PreviousMigration = "20261001220130_AddChildFrameColourList";
    private const string Outlet = OrganisationSeedConfiguration.KenyaRetailPointPath;

    private static readonly Guid BlueBlock = Guid.NewGuid();
    private static readonly Guid Photochromic = Guid.NewGuid();

    private static DotGlassesDbContext CreateContext(string connectionString, string hierarchyPathPrefix = Outlet) =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, hierarchyPathPrefix));

    private static LeadService LeadServiceOver(DotGlassesDbContext context) =>
        new(new LeadRepository(context), new TestRepository(context), new CustomerRepository(context), new CustomOrderRepository(context), context);

    private static SaleService SaleServiceOver(DotGlassesDbContext context) =>
        new(new SaleRepository(context), new LeadRepository(context), new CustomerRepository(context), new CustomOrderRepository(context), context);

    private static CreateLeadRequest AnOrderingLead() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        PhoneNumber = "0700111222",
        AgeYears = 42,
        Gender = Gender.Female,
        ConsentGiven = true,
        ReasonNotPurchasedRefId = Guid.NewGuid(),
        CustomerToldPrice = true,
        LensRangeType = LensRangeType.Custom,
        SphereLeft = -1.25m,
        SphereRight = -2.00m,
        CylinderRight = -0.50m,
        AxisRight = 90m,
        PupilDistanceMm = 62m,
        OrderFromDotGlasses = true,
        CoatingRefIds = [BlueBlock, Photochromic],
    };

    private static CreateSaleRequest ASale(bool order, Guid? sourceLeadId = null) => new()
    {
        Id = Guid.NewGuid(),
        SourceLeadId = sourceLeadId,
        FullName = "Amina Okoro",
        PhoneNumber = "0700111222",
        AgeYears = 42,
        Gender = Gender.Female,
        ConsentGiven = true,
        LensRangeType = LensRangeType.Custom,
        SphereLeft = -1.25m,
        SphereRight = -2.00m,
        CylinderRight = -0.50m,
        AxisRight = 90m,
        PupilDistanceMm = 62m,
        OrderFromDotGlasses = order,
        FrameColourRefId = Guid.NewGuid(),
        FrameCoverage = ContractFrameCoverage.FullFrame,
        CoatingRefIds = [BlueBlock, Photochromic],
    };

    // --- The migration ------------------------------------------------------------------------

    [Fact]
    public async Task EachSaleWithAFulfilmentStatus_BecomesOneOrderWithThatStatus()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = CreateContext(connectionString, OrganisationSeedConfiguration.DgiPath);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        var recordedAt = new DateTimeOffset(2026, 8, 14, 9, 30, 0, TimeSpan.Zero);
        var submitted = Guid.NewGuid();
        var readyForPickup = Guid.NewGuid();
        var fromStock = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        await PreMigrationRows.InsertSaleAsync(context, submitted, Outlet, lensRangeType: 1,
            ("OrderFromDotGlasses", true), ("FulfilmentStatus", (int)FulfilmentStatus.Submitted), ("CreatedAtUtc", recordedAt));
        await PreMigrationRows.InsertSaleAsync(context, readyForPickup, "/1/2/3/5/", lensRangeType: 1,
            ("OrderFromDotGlasses", true), ("FulfilmentStatus", (int)FulfilmentStatus.ReadyForPickup));
        await PreMigrationRows.InsertSaleAsync(context, fromStock, Outlet, lensRangeType: 0);
        await PreMigrationRows.InsertSaleAsync(context, deleted, Outlet, lensRangeType: 1,
            ("OrderFromDotGlasses", true), ("FulfilmentStatus", (int)FulfilmentStatus.InLab), ("IsDeleted", true));

        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();

        var orders = await context.CustomOrders.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(3, orders.Count);
        Assert.Equal(3, orders.Select(o => o.Id).Distinct().Count());
        Assert.All(orders, o => Assert.Null(o.LeadId));

        var first = orders.Single(o => o.SaleId == submitted);
        Assert.Equal(FulfilmentStatus.Submitted, first.Status);
        Assert.Equal(Outlet, first.HierarchyPath);
        Assert.Equal(recordedAt, first.PlacedAtUtc);
        Assert.False(first.IsDeleted);

        var second = orders.Single(o => o.SaleId == readyForPickup);
        Assert.Equal(FulfilmentStatus.ReadyForPickup, second.Status);
        Assert.Equal("/1/2/3/5/", second.HierarchyPath);

        // A Sale handed over from stock never had an order, and still has none.
        Assert.DoesNotContain(orders, o => o.SaleId == fromStock);

        // A deleted Sale's order stays deleted with it rather than reappearing in the queue.
        Assert.True(orders.Single(o => o.SaleId == deleted).IsDeleted);
    }

    [Fact]
    public async Task TheOrderColumnsHaveLeftTheSale()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = CreateContext(connectionString);

        var columns = await context.Database
            .SqlQueryRaw<string>("""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = 'Sales' AND column_name IN ('OrderFromDotGlasses', 'FulfilmentStatus')
                """)
            .ToListAsync();

        Assert.Empty(columns);
    }

    [Fact]
    public async Task MigratingBackDown_PutsAPaidOrderBackOnItsSale()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = CreateContext(connectionString, OrganisationSeedConfiguration.DgiPath);

        var sale = await SaleServiceOver(context).CreateAsync(ASale(order: true), Guid.NewGuid(), Outlet);
        var order = await context.CustomOrders.SingleAsync();
        order.Status = FulfilmentStatus.InLab;
        await context.SaveChangesAsync();

        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        var status = await context.Database
            .SqlQuery<int>($"""SELECT "FulfilmentStatus" AS "Value" FROM "Sales" WHERE "Id" = {sale.Id} AND "OrderFromDotGlasses" """)
            .SingleAsync();
        Assert.Equal((int)FulfilmentStatus.InLab, status);
    }

    // --- Written together, or not at all ------------------------------------------------------

    [Fact]
    public async Task AnOrderingLead_LandsWithItsOrderAndItsCoatingSet()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var request = AnOrderingLead();

        await using (var context = CreateContext(connectionString))
        {
            await LeadServiceOver(context).CreateAsync(request, Guid.NewGuid(), Outlet);
        }

        await using var verify = CreateContext(connectionString);
        var order = await verify.CustomOrders.SingleAsync();
        Assert.Equal(request.Id, order.LeadId);
        Assert.Null(order.SaleId);
        Assert.Equal(Outlet, order.HierarchyPath);
        Assert.Equal(FulfilmentStatus.Submitted, order.Status);
        Assert.Equal(
            new[] { BlueBlock, Photochromic }.Order(),
            (await verify.LeadCoatings.Where(c => c.LeadId == request.Id).Select(c => c.CoatingRefId).ToListAsync()).Order());

        var lead = await LeadServiceOver(verify).GetByIdAsync(request.Id);
        Assert.True(lead!.OrderFromDotGlasses);
        Assert.Equal(CustomOrderStatus.Submitted, lead.CustomOrderStatus);
    }

    [Fact]
    public async Task AnOrderingLeadTheDatabaseRejects_LeavesNoOrderAndNoCoatingsBehind()
    {
        // Forced at the database: the path stamped onto the Lead and its order overflows the
        // column, so the batch fails. Nothing of it may survive — an order for a Lead that does
        // not exist would sit in the lab's queue with no lens to make.
        var connectionString = await postgres.CreateDatabaseAsync();

        await using (var context = CreateContext(connectionString))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => LeadServiceOver(context).CreateAsync(
                AnOrderingLead(), Guid.NewGuid(), $"/{new string('9', 1001)}/"));
        }

        await using var verify = CreateContext(connectionString);
        Assert.Empty(await verify.Leads.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await verify.CustomOrders.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await verify.LeadCoatings.ToListAsync());
        Assert.Empty(await verify.Customers.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task AnOrderingSaleTheDatabaseRejects_LeavesNoOrderBehind()
    {
        var connectionString = await postgres.CreateDatabaseAsync();

        await using (var context = CreateContext(connectionString))
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => SaleServiceOver(context).CreateAsync(
                ASale(order: true), Guid.NewGuid(), $"/{new string('9', 1001)}/"));
        }

        await using var verify = CreateContext(connectionString);
        Assert.Empty(await verify.Sales.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await verify.CustomOrders.IgnoreQueryFilters().ToListAsync());
    }

    // --- Sent twice ---------------------------------------------------------------------------

    [Fact]
    public async Task AnOrderingLeadSentTwice_PlacesOneOrder()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var request = AnOrderingLead();

        // Two requests, as the outbox retrying is: each its own context.
        foreach (var _ in Enumerable.Range(0, 2))
        {
            await using var context = CreateContext(connectionString);
            var lead = await LeadServiceOver(context).CreateAsync(request, Guid.NewGuid(), Outlet);
            Assert.Equal(CustomOrderStatus.Submitted, lead.CustomOrderStatus);
            Assert.Equal(2, lead.CoatingRefIds.Count);
        }

        await using var verify = CreateContext(connectionString);
        Assert.Single(await verify.Leads.ToListAsync());
        Assert.Single(await verify.CustomOrders.ToListAsync());
        Assert.Equal(2, await verify.LeadCoatings.CountAsync());
    }

    [Fact]
    public async Task AnOrderingSaleSentTwice_PlacesOneOrder()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var request = ASale(order: true);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            await using var context = CreateContext(connectionString);
            var sale = await SaleServiceOver(context).CreateAsync(request, Guid.NewGuid(), Outlet);
            Assert.True(sale.OrderFromDotGlasses);
        }

        await using var verify = CreateContext(connectionString);
        Assert.Single(await verify.Sales.ToListAsync());
        Assert.Single(await verify.CustomOrders.ToListAsync());
    }

    // --- Converting an ordered Lead -----------------------------------------------------------

    [Fact]
    public async Task ConvertingAnOrderedLead_LinksTheSaleToTheSameOrder()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var leadRequest = AnOrderingLead();
        await using (var context = CreateContext(connectionString))
        {
            await LeadServiceOver(context).CreateAsync(leadRequest, Guid.NewGuid(), Outlet);
        }

        Guid saleId;
        await using (var context = CreateContext(connectionString))
        {
            var sale = await SaleServiceOver(context).CreateAsync(ASale(order: false, sourceLeadId: leadRequest.Id), Guid.NewGuid(), Outlet);
            saleId = sale.Id;
            Assert.True(sale.OrderFromDotGlasses);
        }

        await using var verify = CreateContext(connectionString);
        var order = await verify.CustomOrders.SingleAsync();
        Assert.Equal(leadRequest.Id, order.LeadId);
        Assert.Equal(saleId, order.SaleId);
    }

    // --- The queue reads the lens through the placing record ----------------------------------

    [Fact]
    public async Task TheQueue_ListsALeadPlacedOrderAsNotYetPaid_AndASalePlacedOneAsPaid()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var leadRequest = AnOrderingLead();
        leadRequest.FullName = "Brian Mwangi";
        leadRequest.PhoneNumber = "0700333444";
        leadRequest.ConsentGiven = false;
        await using (var context = CreateContext(connectionString))
        {
            await LeadServiceOver(context).CreateAsync(leadRequest, Guid.NewGuid(), Outlet);
        }

        await using (var context = CreateContext(connectionString))
        {
            await SaleServiceOver(context).CreateAsync(ASale(order: true), Guid.NewGuid(), Outlet);
        }

        await using var read = CreateContext(connectionString, OrganisationSeedConfiguration.DgiPath);
        var rows = await new CustomOrderService(read, new UnscopedReportQueryService(read)).ExportAsync(status: null);

        var unpaid = Assert.Single(rows, r => !r.IsPaid);
        Assert.Equal("Brian Mwangi", unpaid.CustomerName);
        Assert.False(unpaid.ConsentGiven);
        Assert.Equal("Kangemi Vision Centre — Outreach Post", unpaid.Outlet);

        var paid = Assert.Single(rows, r => r.IsPaid);
        Assert.Equal("Amina Okoro", paid.CustomerName);
        Assert.True(paid.ConsentGiven);

        // The same lens on both records, read from the Lead for one and the Sale for the other.
        Assert.Equal(paid.Prescription, unpaid.Prescription);
        Assert.Contains("-1.25", unpaid.Prescription);
    }

    [Fact]
    public async Task OnceItsLeadConverts_TheOrderIsListedOnceAndAsPaid()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var leadRequest = AnOrderingLead();
        await using (var context = CreateContext(connectionString))
        {
            await LeadServiceOver(context).CreateAsync(leadRequest, Guid.NewGuid(), Outlet);
        }

        await using (var context = CreateContext(connectionString))
        {
            await SaleServiceOver(context).CreateAsync(ASale(order: false, sourceLeadId: leadRequest.Id), Guid.NewGuid(), Outlet);
        }

        await using var read = CreateContext(connectionString, OrganisationSeedConfiguration.DgiPath);
        var result = await new CustomOrderService(read, new UnscopedReportQueryService(read)).ListGroupedAsync(status: null);

        Assert.Equal(1, result.TotalCount);
        var customer = Assert.Single(Assert.Single(Assert.Single(result.Retailers).RetailPoints).Customers);
        Assert.True(Assert.Single(customer.Orders).IsPaid);
    }
}

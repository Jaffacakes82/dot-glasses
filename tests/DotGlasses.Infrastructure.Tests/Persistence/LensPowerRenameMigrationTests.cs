using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Lens-power ticket 01 renamed the per-eye Custom* lens columns on Tests, Leads and Sales to
/// range-neutral names (ADR-0007). A recorded prescription must come through the rename intact —
/// which is what a column rename gives and a drop-and-add would silently lose.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LensPowerRenameMigrationTests(PostgresContainerFixture postgres)
{
    private const string PreviousMigration = "20260928103802_RemoveUserActiveOrg";

    private static readonly (string Column, object? Value)[] RecordedPrescription =
    [
        ("LensRangeType", (int)LensRangeType.Custom),
        ("CustomSphereLeft", -1.25m), ("CustomCylinderLeft", -0.75m), ("CustomAxisLeft", 90m), ("CustomAddPowerLeft", 2.00m),
        ("CustomSphereRight", 0.50m), ("CustomCylinderRight", -1.50m), ("CustomAxisRight", 180m), ("CustomAddPowerRight", 1.75m),
    ];

    [Fact]
    public async Task ARecordedCustomPrescription_SurvivesTheRenameOnTestsLeadsAndSales()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresContainerFixture.CreateContext(connectionString, FakeHttpContextAccessor.Create(hierarchyPathPrefix: "/1/"));
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        var id = Guid.NewGuid();
        await PreMigrationRows.InsertTestAsync(context, id, "/1/2/", Guid.NewGuid(), RecordedPrescription);
        await PreMigrationRows.InsertLeadAsync(context, id, "/1/2/", RecordedPrescription);
        await PreMigrationRows.InsertSaleAsync(context, id, "/1/2/", (int)LensRangeType.Custom, RecordedPrescription[1..]);

        await context.GetService<IMigrator>().MigrateAsync();
        context.ChangeTracker.Clear();

        var test = await context.Tests.SingleAsync(t => t.Id == id);
        var lead = await context.Leads.SingleAsync(l => l.Id == id);
        var sale = await context.Sales.SingleAsync(s => s.Id == id);

        decimal?[] expected = [-1.25m, -0.75m, 90m, 2.00m, 0.50m, -1.50m, 180m, 1.75m];
        Assert.Equal(expected, new[] { test.SphereLeft, test.CylinderLeft, test.AxisLeft, test.AddLeft, test.SphereRight, test.CylinderRight, test.AxisRight, test.AddRight });
        Assert.Equal(expected, new[] { lead.SphereLeft, lead.CylinderLeft, lead.AxisLeft, lead.AddLeft, lead.SphereRight, lead.CylinderRight, lead.AxisRight, lead.AddRight });
        Assert.Equal(expected, new[] { sale.SphereLeft, sale.CylinderLeft, sale.AxisLeft, sale.AddLeft, sale.SphereRight, sale.CylinderRight, sale.AxisRight, sale.AddRight });
    }
}

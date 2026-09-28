using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// ADR-0005 collapsed the 6-Lens/9-Lens lens range types into one "lens set". Records stored
/// before that keep their meaning — the set they used is PresetCatalogueId, which is untouched —
/// but a stored 9-Lens value (1) has no member any more, so the migration must move it.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LensRangeMigrationTests(PostgresContainerFixture postgres)
{
    private const string PreviousMigration = "20260926155259_EnforceUniqueOrganisationPaths";

    /// <summary>The value NineLensSet had before ADR-0005.</summary>
    private const LensRangeType FormerNineLensSet = (LensRangeType)1;

    [Fact]
    public async Task RecordsStoredAsSixOrNineLensBecomeLensSet_AndCustomStaysCustom()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresContainerFixture.CreateContext(connectionString, FakeHttpContextAccessor.Create(hierarchyPathPrefix: "/1/"));
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        var formerSix = Guid.NewGuid();
        var formerNine = Guid.NewGuid();
        var custom = Guid.NewGuid();
        // Raw SQL, not the model: later migrations rename these tables' columns (see PreMigrationRows).
        await PreMigrationRows.InsertSaleAsync(context, formerSix, "/1/2/", (int)LensRangeType.LensSet);
        await PreMigrationRows.InsertSaleAsync(context, formerNine, "/1/2/", (int)FormerNineLensSet);
        await PreMigrationRows.InsertSaleAsync(context, custom, "/1/2/", (int)LensRangeType.Custom);
        await PreMigrationRows.InsertTestAsync(context, formerNine, "/1/2/", Guid.NewGuid(), ("LensRangeType", (int)FormerNineLensSet));
        await PreMigrationRows.InsertLeadAsync(context, formerNine, "/1/2/", ("LensRangeType", (int)FormerNineLensSet));

        await context.GetService<IMigrator>().MigrateAsync();
        context.ChangeTracker.Clear();

        var sales = await context.Sales.ToDictionaryAsync(s => s.Id, s => s.LensRangeType);
        Assert.Equal(LensRangeType.LensSet, sales[formerSix]);
        Assert.Equal(LensRangeType.LensSet, sales[formerNine]);
        Assert.Equal(LensRangeType.Custom, sales[custom]);
        Assert.Equal(LensRangeType.LensSet, (await context.Tests.SingleAsync(t => t.Id == formerNine)).LensRangeType);
        Assert.Equal(LensRangeType.LensSet, (await context.Leads.SingleAsync(l => l.Id == formerNine)).LensRangeType);

        // And the picker role is gone from the table, not just from the model.
        var kindColumns = await context.Database
            .SqlQueryRaw<int>("""SELECT COUNT(*)::int AS "Value" FROM information_schema.columns WHERE table_name = 'PresetCatalogues' AND column_name = 'Kind'""")
            .SingleAsync();
        Assert.Equal(0, kindColumns);
    }
}

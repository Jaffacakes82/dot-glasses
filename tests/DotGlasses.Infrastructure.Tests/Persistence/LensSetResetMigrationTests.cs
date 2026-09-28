using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Lens-power ticket 03 (ADR-0007) resets lens-set data rather than converting it: every lens set
/// is retired and emptied, "Lens strength" items, the global coating availability grid and global
/// pairings go, and coating exclusions stay. History must stay readable — an old lens-set record
/// still names its set, and a custom record keeps what was recorded even where the new allowed
/// values would refuse it.
///
/// The "before" state is the previous migration's, reached by migrating the template down: its
/// Down restores the two seeded lens sets with their seeded lenses, assignments, Lens strength
/// items and grid rows — exactly what a real database held. The rest is written in raw SQL,
/// because the current model no longer maps those tables or columns (see PreMigrationRows).
/// </summary>
[Collection(PostgresCollection.Name)]
public class LensSetResetMigrationTests(PostgresContainerFixture postgres)
{
    private const string PreviousMigration = "20260928105332_RenameLensPowerFieldsRangeNeutral";

    // The seeded rows as they stood before the reset — literals, since the seed configuration
    // that named them is gone.
    private static readonly Guid SeededSixLensSetId = new("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid SeededSixLensPlus250Id = new("d0000000-0000-0000-0000-000000000001");
    private static readonly Guid LensStrengthPlus250Id = new("b0000000-0000-0000-0000-000000000044");
    private static readonly Guid PhotochromicId = new("b0000000-0000-0000-0000-000000000023");
    private static readonly Guid ClearId = new("b0000000-0000-0000-0000-000000000024");
    private static readonly Guid BlueBlockId = new("b0000000-0000-0000-0000-000000000025");
    private static readonly Guid KenyaId = new("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid DgiId = new("a0000000-0000-0000-0000-000000000001");

    private const int LensStrengthCategory = 6;

    private sealed record Before(Guid AdminLensSetId, Guid LensSetSaleId, Guid CustomSaleId, Guid ExclusionId);

    /// <summary>Migrates a fresh database to just before the reset and writes what a real one
    /// held: an admin-built lens set with a lens and an assignment, an admin-added Lens strength
    /// item, a global pairing and an exclusion, a Sale on the seeded 6-Lens set and a custom Sale
    /// recorded before the shop's allowed values applied.</summary>
    private static async Task<Before> ArrangeBeforeTheResetAsync(DotGlassesDbContext context)
    {
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        var now = DateTimeOffset.UtcNow;
        var adminLensSetId = Guid.NewGuid();
        var adminLensId = Guid.NewGuid();
        var adminStrengthId = Guid.NewGuid();
        var exclusionId = Guid.NewGuid();
        var (lower, higher) = ClearId.CompareTo(PhotochromicId) <= 0 ? (ClearId, PhotochromicId) : (PhotochromicId, ClearId);

        await PreMigrationRows.InsertAsync(context, "PresetCatalogues",
            ("Id", adminLensSetId), ("Name", "Kisumu Readers"), ("OwningOrgNodeId", KenyaId), ("CreatedAtUtc", now), ("IsDeleted", false));
        await PreMigrationRows.InsertAsync(context, "PresetCatalogueAssignments",
            ("Id", Guid.NewGuid()), ("PresetCatalogueId", adminLensSetId), ("OrgNodeId", KenyaId), ("CreatedAtUtc", now));
        await PreMigrationRows.InsertAsync(context, "ReferenceDataItems",
            ("Id", adminStrengthId), ("Category", LensStrengthCategory), ("Code", "plus_1_75"), ("Label", "+1.75"), ("SortOrder", 16),
            ("IsActive", true), ("IsOtherOption", false), ("CreatedAtUtc", now), ("IsDeleted", false));
        await PreMigrationRows.InsertAsync(context, "LensOptions",
            ("Id", adminLensId), ("PresetCatalogueId", adminLensSetId), ("LensStrengthRefId", adminStrengthId), ("SortOrder", 0));
        await PreMigrationRows.InsertAsync(context, "LensStrengthCoatingOptions",
            ("Id", Guid.NewGuid()), ("LensStrengthRefId", adminStrengthId), ("CoatingRefId", ClearId), ("CreatedAtUtc", now));
        await PreMigrationRows.InsertAsync(context, "CoatingPairings",
            ("Id", Guid.NewGuid()), ("TriggerCoatingRefId", BlueBlockId), ("PairedCoatingRefId", PhotochromicId), ("CreatedAtUtc", now));
        await PreMigrationRows.InsertAsync(context, "CoatingExclusions",
            ("Id", exclusionId), ("CoatingRefIdA", lower), ("CoatingRefIdB", higher), ("CreatedAtUtc", now));

        var lensSetSaleId = Guid.NewGuid();
        await PreMigrationRows.InsertSaleAsync(context, lensSetSaleId, "/1/2/", (int)LensRangeType.LensSet,
            ("PresetCatalogueId", SeededSixLensSetId), ("LensOptionLeftId", SeededSixLensPlus250Id), ("LensOptionRightId", SeededSixLensPlus250Id),
            ("PresetPupilDistanceBucket", 2));

        // A positive cylinder, a sphere beyond ±10.00 and an add above 3.00: all refused by the
        // shop's allowed values today, all recorded before them.
        var customSaleId = Guid.NewGuid();
        await PreMigrationRows.InsertSaleAsync(context, customSaleId, "/1/2/", (int)LensRangeType.Custom,
            ("SphereLeft", 12.50m), ("CylinderLeft", 1.00m), ("AxisLeft", 45m), ("AddLeft", 3.50m),
            ("SphereRight", -11.00m), ("CylinderRight", -7.25m), ("AxisRight", 180m), ("AddRight", 3.50m));

        return new Before(adminLensSetId, lensSetSaleId, customSaleId, exclusionId);
    }

    /// <summary>The caller disposes the context.</summary>
    private async Task<(DotGlassesDbContext Context, Before Before)> MigratedPastTheResetAsync()
    {
        var context = PostgresContainerFixture.CreateContext(await postgres.CreateDatabaseAsync(), FakeHttpContextAccessor.Create(hierarchyPathPrefix: "/1/"));
        var before = await ArrangeBeforeTheResetAsync(context);
        await context.GetService<IMigrator>().MigrateAsync();
        context.ChangeTracker.Clear();
        return (context, before);
    }

    private static Task<int> CountAsync(DotGlassesDbContext context, string sql) =>
        context.Database.SqlQueryRaw<int>(sql).SingleAsync();

    [Fact]
    public async Task EveryLensSetEndsRetiredAndEmpty_WithNoAssignments()
    {
        var (context, before) = await MigratedPastTheResetAsync();
        await using var disposeContext = context;

        var lensSets = await context.PresetCatalogues.IgnoreQueryFilters().ToListAsync();
        Assert.Contains(lensSets, c => c.Id == SeededSixLensSetId);
        Assert.Contains(lensSets, c => c.Id == before.AdminLensSetId);
        Assert.All(lensSets, c =>
        {
            Assert.True(c.IsDeleted, $"{c.Name} should be retired");
            Assert.NotNull(c.DeletedAtUtc);
        });

        // Retired, not deleted: the seeded sets survive by id and keep their names.
        Assert.Equal("6-Lens Set", lensSets.Single(c => c.Id == SeededSixLensSetId).Name);
        Assert.Equal("Kisumu Readers", lensSets.Single(c => c.Id == before.AdminLensSetId).Name);

        Assert.Empty(await context.LensOptions.ToListAsync());
        Assert.Empty(await context.PresetCatalogueAssignments.ToListAsync());
    }

    [Fact]
    public async Task LensStrengthItems_TheGrid_AndGlobalPairingsAreGone_ExclusionsStay()
    {
        var (context, before) = await MigratedPastTheResetAsync();
        await using var disposeContext = context;

        // Seeded and admin-added alike.
        Assert.Equal(0, await CountAsync(context, $"""SELECT COUNT(*)::int AS "Value" FROM "ReferenceDataItems" WHERE "Category" = {LensStrengthCategory}"""));
        Assert.Equal(0, await CountAsync(context,
            """SELECT COUNT(*)::int AS "Value" FROM information_schema.tables WHERE table_name IN ('LensStrengthCoatingOptions', 'CoatingPairings')"""));

        var exclusion = Assert.Single(await context.CoatingExclusions.ToListAsync());
        Assert.Equal(before.ExclusionId, exclusion.Id);

        // Nothing else in the library moved.
        Assert.Equal(6, await context.ReferenceDataItems.CountAsync(x => x.Category == ReferenceDataCategory.Coating));
        Assert.Equal(3, await context.ReferenceDataItems.CountAsync(x => x.Category == ReferenceDataCategory.LensType));
    }

    [Fact]
    public async Task AnOldLensSetRecordStillResolvesItsSetsName()
    {
        var (context, before) = await MigratedPastTheResetAsync();
        await using var disposeContext = context;

        var sale = await context.Sales.SingleAsync(s => s.Id == before.LensSetSaleId);
        Assert.Equal(LensRangeType.LensSet, sale.LensRangeType);
        Assert.Equal(SeededSixLensSetId, sale.PresetCatalogueId);

        var snapshot = await new ReferenceDataSnapshotProvider(context, new UnscopedReportQueryService(context)).GetAsync();
        var lensSet = snapshot.FindCatalogue(sale.PresetCatalogueId);
        Assert.NotNull(lensSet);
        Assert.Equal("6-Lens Set", lensSet.Name);
        Assert.False(lensSet.IsActive);

        // Its lens is gone with the set's contents; the record says so honestly rather than
        // throwing (the lens power it was sold with is not recoverable from a label).
        Assert.Equal("—", snapshot.ResolveLensOptionLabel(sale.LensOptionLeftId));
    }

    [Fact]
    public async Task AnOldCustomRecordKeepsItsValues_EvenOutsideTheAllowedRanges()
    {
        var (context, before) = await MigratedPastTheResetAsync();
        await using var disposeContext = context;

        var sale = await context.Sales.SingleAsync(s => s.Id == before.CustomSaleId);

        Assert.Equal(LensRangeType.Custom, sale.LensRangeType);
        Assert.Equal(
            new decimal?[] { 12.50m, 1.00m, 45m, 3.50m, -11.00m, -7.25m, 180m, 3.50m },
            new[] { sale.SphereLeft, sale.CylinderLeft, sale.AxisLeft, sale.AddLeft, sale.SphereRight, sale.CylinderRight, sale.AxisRight, sale.AddRight });
    }
}

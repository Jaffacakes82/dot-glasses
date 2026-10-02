using DotGlasses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Tests.Postgres;

/// <summary>
/// Writes Test/Lead/Sale rows in raw SQL, for a migration test that has migrated to an earlier
/// schema and needs "before" rows there. Going through the current model instead breaks as soon as
/// any later migration renames or drops one of these tables' columns, because EF inserts every
/// mapped column (that is what lens-power ticket 01's rename did to the two tests that used to).
///
/// Only the NOT NULL columns are filled; <paramref name="extra"/> names any others the test cares
/// about, spelled as the columns were at that earlier schema.
/// </summary>
public static class PreMigrationRows
{
    public static Task InsertTestAsync(DotGlassesDbContext context, Guid id, string hierarchyPath, Guid technicianUserId, params (string Column, object? Value)[] extra) =>
        InsertAsync(context, "Tests",
        [
            ("Id", id), ("HierarchyPath", hierarchyPath), ("TechnicianUserId", technicianUserId),
            ("Gender", 0), ("Outcome", 0), ("ReferredOrTreated", false), ("TreatedInFacility", false),
            ("ChildrensFrame", false), ("CreatedAtUtc", DateTimeOffset.UtcNow), ("IsDeleted", false),
            .. extra,
        ]);

    public static Task InsertLeadAsync(DotGlassesDbContext context, Guid id, string hierarchyPath, params (string Column, object? Value)[] extra) =>
        InsertAsync(context, "Leads",
        [
            ("Id", id), ("HierarchyPath", hierarchyPath), ("TechnicianUserId", Guid.NewGuid()), ("CustomerId", Guid.NewGuid()),
            ("Gender", 0), ("ConsentGiven", true), ("ConvertedFlag", false), ("ReasonNotPurchasedRefId", Guid.Empty),
            ("ReferredOrTreated", false), ("TreatedInFacility", false), ("ChildrensFrame", false),
            ("CreatedAtUtc", DateTimeOffset.UtcNow), ("IsDeleted", false),
            .. extra,
        ]);

    public static Task InsertSaleAsync(DotGlassesDbContext context, Guid id, string hierarchyPath, int lensRangeType, params (string Column, object? Value)[] extra) =>
        InsertAsync(context, "Sales",
        [
            ("Id", id), ("HierarchyPath", hierarchyPath), ("TechnicianUserId", Guid.NewGuid()), ("CustomerId", Guid.NewGuid()),
            ("Gender", 0), ("ConsentGiven", true), ("FrameColourRefId", Guid.Empty), ("FrameCoverage", 0),
            ("HardCaseSold", false), ("LensRangeType", lensRangeType), ("OrderFromDotGlasses", false),
            ("ReferredOrTreated", false), ("TreatedInFacility", false), ("ChildrensFrame", false),
            ("CreatedAtUtc", DateTimeOffset.UtcNow), ("IsDeleted", false),
            .. extra,
        ]);

    /// <summary>Any other table's row, for a table the current model no longer maps or maps
    /// differently (lens-power ticket 03 removed LensStrengthCoatingOptions and CoatingPairings, and
    /// reshaped LensOptions). Every NOT NULL column must be given. A column named twice takes the
    /// later value, so a test's <c>extra</c> can override one of the defaults above.</summary>
    public static async Task InsertAsync(DotGlassesDbContext context, string table, params (string Column, object? Value)[] values)
    {
        values = values.GroupBy(v => v.Column).Select(g => g.Last()).ToArray();
        var columns = string.Join(", ", values.Select(v => $"\"{v.Column}\""));
        var placeholders = string.Join(", ", values.Select((_, i) => $"{{{i}}}"));
#pragma warning disable EF1002 // Column and table names are this helper's own literals, never input; values are parameters.
        await context.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"{table}\" ({columns}) VALUES ({placeholders})",
            values.Select(v => v.Value ?? DBNull.Value).ToArray());
#pragma warning restore EF1002
    }
}

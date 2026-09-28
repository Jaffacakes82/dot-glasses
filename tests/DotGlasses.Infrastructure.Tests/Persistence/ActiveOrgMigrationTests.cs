using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// ADR-0006 removed the user's single "active org" (AspNetUsers.OrgNodeId/HierarchyPath/OrgLevel).
/// Until then that org counted as one of the user's assignments even with no UserOrgAssignment
/// row behind it, so the migration that drops the columns must first turn it into a real
/// assignment — or the user silently loses that scope the moment it ships (user story 41).
///
/// The model no longer has the columns, so the "before" rows are written in raw SQL against the
/// schema as it stood at the previous migration.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ActiveOrgMigrationTests(PostgresContainerFixture postgres)
{
    private const string PreviousMigration = "20260926170020_DropPresetCatalogueRangeDescription";

    [Fact]
    public async Task AUsersFormerActiveOrg_IsKeptAsOneOfTheirAssignments()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresContainerFixture.CreateContext(connectionString, FakeHttpContextAccessor.Create(hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath));
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

        // Active org with no assignment row: the case that would lose access.
        var unbacked = await InsertUserAsync(context, OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointPath, level: 3);

        // Active org already backed by an assignment row, plus a second assignment elsewhere.
        var backed = await InsertUserAsync(context, OrganisationSeedConfiguration.KenyaId, OrganisationSeedConfiguration.KenyaPath, level: 1);
        await InsertAssignmentAsync(context, backed, OrganisationSeedConfiguration.KenyaId);
        await InsertAssignmentAsync(context, backed, OrganisationSeedConfiguration.DgiId);

        // No active org at all.
        var unassigned = await InsertUserAsync(context, orgNodeId: null, hierarchyPath: "", level: null);

        // A record stamped above retail-point level, which the upgrade must leave where it is.
        var dgiLevelTest = Guid.NewGuid();
        await PreMigrationRows.InsertTestAsync(context, dgiLevelTest, OrganisationSeedConfiguration.DgiPath, backed);

        await context.GetService<IMigrator>().MigrateAsync();
        context.ChangeTracker.Clear();

        Assert.Equal([OrganisationSeedConfiguration.KenyaRetailPointId], await AssignedOrgIdsAsync(context, unbacked));
        Assert.Equal(
            new[] { OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.KenyaId }.Order(),
            (await AssignedOrgIdsAsync(context, backed)).Order());
        Assert.Empty(await AssignedOrgIdsAsync(context, unassigned));

        Assert.Equal(OrganisationSeedConfiguration.DgiPath, (await context.Tests.SingleAsync(t => t.Id == dgiLevelTest)).HierarchyPath);
    }

    [Fact]
    public async Task TheActiveOrgColumnsAndTheirForeignKeyAreGone()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = PostgresContainerFixture.CreateContext(connectionString);

        var columns = await context.Database
            .SqlQueryRaw<string>("""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_name = 'AspNetUsers' AND column_name IN ('OrgNodeId', 'HierarchyPath', 'OrgLevel')
                """)
            .ToListAsync();
        Assert.Empty(columns);

        var foreignKeys = await context.Database
            .SqlQueryRaw<string>("""
                SELECT constraint_name AS "Value" FROM information_schema.table_constraints
                WHERE table_name = 'AspNetUsers' AND constraint_type = 'FOREIGN KEY'
                """)
            .ToListAsync();
        Assert.Empty(foreignKeys);
    }

    private static async Task<Guid> InsertUserAsync(DotGlassesDbContext context, Guid? orgNodeId, string hierarchyPath, int? level)
    {
        var id = Guid.NewGuid();
        var userName = $"user-{id:N}@test.local";
        var normalized = userName.ToUpperInvariant();
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "AspNetUsers"
                ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
                 "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount",
                 "OrgNodeId", "HierarchyPath", "OrgLevel")
            VALUES ({id}, {userName}, {normalized}, {userName}, {normalized}, TRUE,
                    FALSE, FALSE, TRUE, 0,
                    {orgNodeId}, {hierarchyPath}, {level})
            """);
        return id;
    }

    private static async Task InsertAssignmentAsync(DotGlassesDbContext context, Guid userId, Guid orgNodeId) =>
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "UserOrgAssignments" ("Id", "UserId", "OrgNodeId", "CreatedAtUtc")
            VALUES ({Guid.NewGuid()}, {userId}, {orgNodeId}, {DateTimeOffset.UtcNow})
            """);

    private static async Task<List<Guid>> AssignedOrgIdsAsync(DotGlassesDbContext context, Guid userId) =>
        await context.UserOrgAssignments.Where(a => a.UserId == userId).Select(a => a.OrgNodeId).ToListAsync();
}

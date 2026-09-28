using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Persistence.Interceptors;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// A HierarchyPath segment, once minted, belongs to that node for good: deactivating a node keeps
/// its path (it can be reactivated), so its segments are never free to hand out again. Two nodes
/// sharing a path collapse two orgs' data scopes into one and take down every OrgTreeLookup-backed
/// screen (Dashboard, Event History, Custom Orders) for every user — found live as a duplicate
/// "/1/2/" after the seeded Kenya chain was deactivated and a new country created.
/// </summary>
[Collection(PostgresCollection.Name)]
public class OrganisationPathMintingTests(PostgresContainerFixture postgres)
{
    private static DotGlassesDbContext CreateContext(string connectionString)
    {
        var currentUser = new FakeCurrentUserContext { ScopePaths = [HierarchyPath.Parse(OrganisationSeedConfiguration.DgiPath)] };
        return PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, OrganisationSeedConfiguration.DgiPath),
            new AuditSaveChangesInterceptor(currentUser));
    }

    private static OrganisationAdminService CreateService(DotGlassesDbContext context) =>
        new(context, new FakeCurrentUserContext { ScopePaths = [HierarchyPath.Parse(OrganisationSeedConfiguration.DgiPath)] });

    [Fact]
    public async Task ADeactivatedNodesPathIsNeverHandedToANewNode()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = CreateContext(connectionString);
        var service = CreateService(context);

        var first = await service.CreateChildAsync(OrganisationSeedConfiguration.KenyaId, "Mombasa Outlet", OrganisationLevel.RetailPoint, "Standalone");
        await service.SetActiveAsync(first.Id, isActive: false);

        var second = await service.CreateChildAsync(OrganisationSeedConfiguration.KenyaId, "Kisumu Outlet", OrganisationLevel.RetailPoint, "Standalone");

        Assert.NotEqual(first.HierarchyPath, second.HierarchyPath);

        // Reactivating the first is what turned the live duplicate from latent into a crash.
        await service.SetActiveAsync(first.Id, isActive: true);

        var active = await CreateService(context).ListAsync();
        Assert.Contains(active, n => n.Id == first.Id && n.HierarchyPath == first.HierarchyPath);
        Assert.Contains(active, n => n.Id == second.Id && n.HierarchyPath == second.HierarchyPath);
    }

    [Fact]
    public async Task ANewCountryAfterTheWholeSeededChainIsDeactivated_GetsASegmentAboveEveryExistingOne()
    {
        // The live incident: with Kenya (/1/2/) and everything beneath it deactivated, the only
        // active segment left was DGI's 1, so the next country was handed /1/2/ again.
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = CreateContext(connectionString);
        var service = CreateService(context);

        await service.SetActiveAsync(OrganisationSeedConfiguration.KenyaRetailPointId, isActive: false);
        await service.SetActiveAsync(OrganisationSeedConfiguration.KenyaRetailerId, isActive: false);
        await service.SetActiveAsync(OrganisationSeedConfiguration.KenyaId, isActive: false);

        var uganda = await service.CreateChildAsync(OrganisationSeedConfiguration.DgiId, "Uganda", OrganisationLevel.Country, kind: null);

        // The seeded tree's highest segment is the 4 in /1/2/3/4/.
        Assert.True(LastSegment(uganda.HierarchyPath) > 4, $"Expected a segment above 4, got {uganda.HierarchyPath}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheDatabaseRefusesASecondNodeOnAnExistingPath_WhetherOrNotTheFirstIsActive(bool existingIsActive)
    {
        // The backstop behind the sequence: whatever writes an OrganisationNode, the table itself
        // won't hold two on one path. A deactivated holder counts — it can be reactivated.
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = CreateContext(connectionString);

        if (!existingIsActive)
        {
            var service = CreateService(context);
            await service.SetActiveAsync(OrganisationSeedConfiguration.KenyaRetailPointId, isActive: false);
        }

        context.OrganisationNodes.Add(new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = OrganisationSeedConfiguration.KenyaRetailerId,
            Name = "Duplicate Outlet",
            Level = OrganisationLevel.RetailPoint,
            HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath,
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [Fact]
    public async Task MigratingADatabaseThatAlreadyHoldsDuplicatePaths_FailsNamingThePathAndTheRepair()
    {
        // What CI's `dotnet ef database update` meets in an environment corrupted before this fix:
        // it must stop with something actionable, not a bare index error — and change nothing.
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration);
        await context.Database.ExecuteSqlRawAsync($"""
            INSERT INTO "OrganisationNodes" ("Id", "ParentId", "Name", "Level", "HierarchyPath", "IsTrainingOrg", "IsDeleted", "CreatedAtUtc")
            VALUES ('{Guid.NewGuid()}', '{OrganisationSeedConfiguration.DgiId}', 'Second Kenya', {(int)OrganisationLevel.Country}, '{OrganisationSeedConfiguration.KenyaPath}', false, false, now());
            """);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());

        Assert.Contains(OrganisationSeedConfiguration.KenyaPath, ex.MessageText);
        Assert.Contains("Duplicate organisation paths", ex.MessageText);
        Assert.Contains(PreviousMigration, await context.Database.GetAppliedMigrationsAsync());
        Assert.DoesNotContain(ThisMigration, await context.Database.GetAppliedMigrationsAsync());
    }

    private const string PreviousMigration = "20260910071312_AddUserOrgAssignmentForeignKeys";

    private const string ThisMigration = "20260926155259_EnforceUniqueOrganisationPaths";

    private static int LastSegment(string hierarchyPath) =>
        int.Parse(hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries)[^1]);
}

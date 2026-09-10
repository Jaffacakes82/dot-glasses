using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// The global hierarchy query filter (DotGlassesDbContext.BuildQueryFilterGeneric) against a real
/// Postgres StartsWith translation — specifically the empty-prefix edge case. "anything".StartsWith("")
/// is always true in .NET, so a caller whose HierarchyPath claim is "" (an authenticated user with
/// no org assignment — see DevUserSeeder's documented legacy-account case) would otherwise match
/// every row instead of none, turning an unassigned account into one that can read every outlet's
/// data. The filter must fail closed instead.
/// </summary>
[Collection(PostgresCollection.Name)]
public class HierarchyScopingFilterTests(PostgresContainerFixture postgres)
{
    private const string CallerOutlet = OrganisationSeedConfiguration.KenyaRetailPointPath;

    [Fact]
    public async Task ACallerWithNoOrgAssignment_SeesNoHierarchyScopedRowsAtAll()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context => context.Tests.Add(NewTest(CallerOutlet)));

        await using var unassignedContext = CreateContext(connectionString, hierarchyPathPrefix: "");

        Assert.Empty(await unassignedContext.Tests.ToListAsync());
    }

    [Fact]
    public async Task ACallerScopedAtTheRoot_StillSeesEveryRow()
    {
        // Contrast case: the fix must distinguish "no assignment" (prefix "") from "assigned at
        // the root" (prefix "/1/") — only the former should come back empty.
        var connectionString = await postgres.CreateDatabaseAsync();
        await SeedAsync(connectionString, context => context.Tests.Add(NewTest(CallerOutlet)));

        await using var rootContext = CreateContext(connectionString, hierarchyPathPrefix: OrganisationSeedConfiguration.DgiPath);

        Assert.Single(await rootContext.Tests.ToListAsync());
    }

    private static DotGlassesDbContext CreateContext(string connectionString, string hierarchyPathPrefix) =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, hierarchyPathPrefix));

    private static async Task SeedAsync(string connectionString, Action<DotGlassesDbContext> seed)
    {
        await using var context = CreateContext(connectionString, CallerOutlet);
        seed(context);
        await context.SaveChangesAsync();
    }

    private static Test NewTest(string hierarchyPath) => new()
    {
        Id = Guid.NewGuid(),
        HierarchyPath = hierarchyPath,
        TechnicianUserId = Guid.NewGuid(),
    };
}

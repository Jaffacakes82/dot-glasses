using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Path-prefix matching is the security-critical piece of the data-scoping global query
/// filter (brief 3.2a) — these tests exercise root/leaf/sibling-prefix edge cases directly
/// against the filter, and prove the filter is re-evaluated per DbContext instance (i.e. two
/// contexts built with different "current user" claims see different rows from the same data).
/// </summary>
public class HierarchyQueryFilterTests
{
    private static DotGlassesDbContext CreateContext(string databaseName, bool isAuthenticated = true, string hierarchyPathPrefix = "")
    {
        var options = new DbContextOptionsBuilder<DotGlassesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new DotGlassesDbContext(options, FakeHttpContextAccessor.Create(isAuthenticated, hierarchyPathPrefix));
    }

    private static async Task SeedAsync(string databaseName)
    {
        await using var seedContext = CreateContext(databaseName);

        seedContext.WidgetExamples.AddRange(
            new WidgetExample { Id = Guid.NewGuid(), Name = "Root", HierarchyPath = "/1/" },
            new WidgetExample { Id = Guid.NewGuid(), Name = "Child", HierarchyPath = "/1/4/" },
            new WidgetExample { Id = Guid.NewGuid(), Name = "Grandchild (leaf)", HierarchyPath = "/1/4/12/" },
            // Shares the string prefix "/1/4" with Child/Grandchild but is NOT a descendant of
            // "/1/4/" — the classic sibling-prefix bug a naive (non-trailing-slash) match would
            // fall for: node "/1/40/" vs. prefix "/1/4/".
            new WidgetExample { Id = Guid.NewGuid(), Name = "Sibling", HierarchyPath = "/1/40/" });

        await seedContext.SaveChangesAsync();
    }

    [Fact]
    public async Task RootPrefix_ReturnsEveryDescendantIncludingSelf()
    {
        var dbName = Guid.NewGuid().ToString();
        await SeedAsync(dbName);

        await using var context = CreateContext(dbName, hierarchyPathPrefix: "/1/");

        var names = await context.WidgetExamples.Select(w => w.Name).ToListAsync();

        Assert.Equal(["Child", "Grandchild (leaf)", "Root", "Sibling"], names.OrderBy(n => n));
    }

    [Fact]
    public async Task LeafPrefix_ReturnsOnlyThatLeaf()
    {
        var dbName = Guid.NewGuid().ToString();
        await SeedAsync(dbName);

        await using var context = CreateContext(dbName, hierarchyPathPrefix: "/1/4/12/");

        var names = await context.WidgetExamples.Select(w => w.Name).ToListAsync();

        Assert.Equal(["Grandchild (leaf)"], names);
    }

    [Fact]
    public async Task SiblingPathSharingStringPrefix_IsExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        await SeedAsync(dbName);

        await using var context = CreateContext(dbName, hierarchyPathPrefix: "/1/4/");

        var names = await context.WidgetExamples.Select(w => w.Name).ToListAsync();

        Assert.Equal(["Child", "Grandchild (leaf)"], names.OrderBy(n => n));
        Assert.DoesNotContain("Sibling", names);
    }

    [Fact]
    public async Task Unauthenticated_SeesNothing_RegardlessOfPrefix()
    {
        var dbName = Guid.NewGuid().ToString();
        await SeedAsync(dbName);

        await using var context = CreateContext(dbName, isAuthenticated: false, hierarchyPathPrefix: "/1/");

        var names = await context.WidgetExamples.ToListAsync();

        Assert.Empty(names);
    }

    [Fact]
    public async Task DifferentContextInstances_SeeDifferentRows_FromTheSameUnderlyingData()
    {
        // Both instances alive at once deliberately — this is the normal steady state of a web
        // server under concurrent load (two different users' request-scoped DbContext
        // instances, live at the same time). A property-rooted (rather than field-rooted)
        // query filter expression previously broke exactly this case — different concurrently
        // alive instances would bleed each other's rows — so this is a regression test for that,
        // not just a demonstration.
        var dbName = Guid.NewGuid().ToString();
        await SeedAsync(dbName);

        await using var leafScoped = CreateContext(dbName, hierarchyPathPrefix: "/1/4/12/");
        await using var rootScoped = CreateContext(dbName, hierarchyPathPrefix: "/1/");

        var leafCount = (await leafScoped.WidgetExamples.ToListAsync()).Count;
        var rootCount = (await rootScoped.WidgetExamples.ToListAsync()).Count;

        Assert.Equal(1, leafCount);
        Assert.Equal(4, rootCount);
    }
}

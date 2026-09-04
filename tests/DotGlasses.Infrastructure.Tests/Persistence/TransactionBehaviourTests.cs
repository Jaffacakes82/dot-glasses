using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Pins the transactional guarantees the conversion services (Test -> Lead -> Sale) claim in
/// comments but have never had asserted: a batch of writes lands whole or not at all.
///
/// These tests are the reason this assembly moved off the EF Core InMemory provider. That
/// provider has no transaction implementation at all — BeginTransaction is a no-op that warns,
/// a rollback discards nothing, and a constraint violation never happens because there are no
/// constraints — so every assertion below would have passed vacuously or failed for the wrong
/// reason. They exist here as the harness's own proof that the capability is now real, and as
/// the foundation the conversion-atomicity work depends on.
/// </summary>
[Collection(PostgresCollection.Name)]
public class TransactionBehaviourTests(PostgresContainerFixture postgres)
{
    private static DotGlassesDbContext CreateContext(string connectionString) =>
        PostgresContainerFixture.CreateContext(connectionString, FakeHttpContextAccessor.Create(hierarchyPathPrefix: "/1/"));

    private static WidgetExample Widget(Guid id, string name) =>
        new() { Id = id, Name = name, HierarchyPath = "/1/" };

    [Fact]
    public async Task ExplicitRollback_DiscardsEveryWriteMadeInsideTheTransaction()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await using (var context = CreateContext(connectionString))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            context.WidgetExamples.AddRange(Widget(first, "First"), Widget(second, "Second"));
            await context.SaveChangesAsync();

            // Visible to this connection inside the open transaction...
            Assert.Equal(2, await context.WidgetExamples.CountAsync());

            await transaction.RollbackAsync();
        }

        // ...and gone entirely once it is rolled back.
        await using var verifyContext = CreateContext(connectionString);
        Assert.Empty(await verifyContext.WidgetExamples.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task ExplicitCommit_PersistsEveryWriteMadeInsideTheTransaction()
    {
        var connectionString = await postgres.CreateDatabaseAsync();

        await using (var context = CreateContext(connectionString))
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            context.WidgetExamples.AddRange(Widget(Guid.NewGuid(), "First"), Widget(Guid.NewGuid(), "Second"));
            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }

        await using var verifyContext = CreateContext(connectionString);
        Assert.Equal(2, await verifyContext.WidgetExamples.CountAsync());
    }

    [Fact]
    public async Task SaveChanges_IsAtomic_AFailingRowDiscardsTheValidRowsAlongsideIt()
    {
        // The guarantee the conversion services rest on: they Add/Update several entities and
        // call IUnitOfWork.SaveChangesAsync once, relying on EF's implicit transaction around
        // the batch. Here a second insert collides with an existing primary key, so the whole
        // batch — including the perfectly valid row — must be discarded.
        var connectionString = await postgres.CreateDatabaseAsync();
        var existing = Guid.NewGuid();

        await using (var seedContext = CreateContext(connectionString))
        {
            seedContext.WidgetExamples.Add(Widget(existing, "Existing"));
            await seedContext.SaveChangesAsync();
        }

        var wouldHaveBeenValid = Guid.NewGuid();

        await using (var context = CreateContext(connectionString))
        {
            context.WidgetExamples.Add(Widget(wouldHaveBeenValid, "Valid"));
            context.WidgetExamples.Add(Widget(existing, "Duplicate primary key"));

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using var verifyContext = CreateContext(connectionString);
        var names = await verifyContext.WidgetExamples.Select(w => w.Name).ToListAsync();
        Assert.Equal(["Existing"], names);
    }
}

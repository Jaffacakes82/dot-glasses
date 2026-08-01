using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Interceptors;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Tests.Persistence;

public class AuditSaveChangesInterceptorTests
{
    private static DotGlassesDbContext CreateContext(string databaseName, FakeCurrentUserContext currentUserContext)
    {
        var options = new DbContextOptionsBuilder<DotGlassesDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new AuditSaveChangesInterceptor(currentUserContext))
            .Options;

        // The interceptor (audit fields: who) still takes ICurrentUserContext directly — it's
        // registered scoped in Web, unaffected by the DbContext-pooling constraint that made
        // DotGlassesDbContext itself switch to IHttpContextAccessor for the query filter.
        var httpContextAccessor = FakeHttpContextAccessor.Create(isAuthenticated: true, currentUserContext.HierarchyPathPrefix, currentUserContext.UserName ?? "test-user");
        return new DotGlassesDbContext(options, httpContextAccessor);
    }

    [Fact]
    public async Task Adding_PopulatesCreatedFields_NotModifiedFields()
    {
        var currentUser = new FakeCurrentUserContext { UserName = "alice", HierarchyPathPrefix = "/1/" };
        await using var context = CreateContext(Guid.NewGuid().ToString(), currentUser);

        var widget = new WidgetExample { Id = Guid.NewGuid(), Name = "Test", HierarchyPath = "/1/" };
        context.WidgetExamples.Add(widget);
        await context.SaveChangesAsync();

        Assert.Equal("alice", widget.CreatedBy);
        Assert.True(widget.CreatedAtUtc > DateTimeOffset.MinValue);
        Assert.Null(widget.ModifiedBy);
        Assert.Null(widget.ModifiedAtUtc);
    }

    [Fact]
    public async Task Modifying_PopulatesModifiedFields_LeavesCreatedFieldsIntact()
    {
        var dbName = Guid.NewGuid().ToString();
        var creator = new FakeCurrentUserContext { UserName = "alice", HierarchyPathPrefix = "/1/" };
        var widgetId = Guid.NewGuid();

        await using (var createContext = CreateContext(dbName, creator))
        {
            createContext.WidgetExamples.Add(new WidgetExample { Id = widgetId, Name = "Test", HierarchyPath = "/1/" });
            await createContext.SaveChangesAsync();
        }

        var editor = new FakeCurrentUserContext { UserName = "bob", HierarchyPathPrefix = "/1/" };
        await using var editContext = CreateContext(dbName, editor);
        var widget = await editContext.WidgetExamples.SingleAsync(w => w.Id == widgetId);
        widget.Name = "Renamed";
        await editContext.SaveChangesAsync();

        Assert.Equal("alice", widget.CreatedBy);
        Assert.Equal("bob", widget.ModifiedBy);
        Assert.NotNull(widget.ModifiedAtUtc);
    }

    [Fact]
    public async Task Removing_IsRewrittenAsSoftDelete_RowStillPresentButFlagged()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUser = new FakeCurrentUserContext { UserName = "alice", HierarchyPathPrefix = "/1/" };
        var widgetId = Guid.NewGuid();

        await using (var createContext = CreateContext(dbName, currentUser))
        {
            createContext.WidgetExamples.Add(new WidgetExample { Id = widgetId, Name = "Test", HierarchyPath = "/1/" });
            await createContext.SaveChangesAsync();
        }

        await using (var deleteContext = CreateContext(dbName, currentUser))
        {
            var widget = await deleteContext.WidgetExamples.SingleAsync(w => w.Id == widgetId);
            deleteContext.WidgetExamples.Remove(widget);
            await deleteContext.SaveChangesAsync();
        }

        // The normal (filtered) query no longer sees it — IsDeleted rows are excluded — but the
        // row itself must still physically exist (a soft delete, not a hard delete).
        await using var verifyContext = CreateContext(dbName, currentUser);
        Assert.False(await verifyContext.WidgetExamples.AnyAsync(w => w.Id == widgetId));

        var stillThere = await verifyContext.WidgetExamples.IgnoreQueryFilters().SingleAsync(w => w.Id == widgetId);
        Assert.True(stillThere.IsDeleted);
        Assert.Equal("alice", stillThere.DeletedBy);
        Assert.NotNull(stillThere.DeletedAtUtc);
    }
}

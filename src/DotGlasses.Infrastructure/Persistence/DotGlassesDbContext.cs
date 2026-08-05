using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class DotGlassesDbContext(DbContextOptions<DotGlassesDbContext> options, IHttpContextAccessor httpContextAccessor)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IUnitOfWork
{
    // Depends on IHttpContextAccessor (singleton) rather than the scoped ICurrentUserContext
    // directly: DotGlasses.Web registers this DbContext via Aspire's AddNpgsqlDbContext, which
    // pools DbContext instances (AddDbContextPool) — a pooled context's constructor is built
    // once from the root provider, so a scoped constructor dependency fails to resolve at
    // startup ("Cannot resolve scoped service ... from root provider"). IHttpContextAccessor
    // has no such problem, and its .HttpContext is still correctly per-request (AsyncLocal).
    //
    // Referenced by name via reflection in BuildQueryFilterGeneric below, and specifically as a
    // FIELD (not a computed property) at the root of the filter expression: EF Core's
    // per-DbContext-instance re-evaluation of a captured query-filter value only kicks in for a
    // MemberExpression whose root is a field read directly off Expression.Constant(this) —
    // routing through a property on the DbContext instead breaks that re-evaluation under
    // concurrently-alive DbContext instances (verified with a regression test: two DbContext
    // instances alive at once, for different users, started returning each other's rows).
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public DbSet<WidgetExample> WidgetExamples => Set<WidgetExample>();

    public DbSet<OrganisationNode> OrganisationNodes => Set<OrganisationNode>();
    public DbSet<UserOrgAssignment> UserOrgAssignments => Set<UserOrgAssignment>();
    public DbSet<ReferenceDataItem> ReferenceDataItems => Set<ReferenceDataItem>();
    public DbSet<PresetCatalogue> PresetCatalogues => Set<PresetCatalogue>();
    public DbSet<PresetCatalogueAssignment> PresetCatalogueAssignments => Set<PresetCatalogueAssignment>();
    public DbSet<LensOption> LensOptions => Set<LensOption>();
    public DbSet<LensStrengthCoatingOption> LensStrengthCoatingOptions => Set<LensStrengthCoatingOption>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Sale> Sales => Set<Sale>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DotGlassesDbContext).Assembly);

        ApplyGlobalQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies the data-scoping (IHierarchyScoped) and soft-delete (ISoftDeletable) global
    /// query filters to every entity that implements them, combining both when an entity
    /// implements both — see CLAUDE.md: this is deliberately separate from RBAC, which lives
    /// in DotGlasses.Web as policy-based authorization and never touches these filters.
    /// </summary>
    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);
            var isHierarchyScoped = typeof(IHierarchyScoped).IsAssignableFrom(clrType);

            if (!isSoftDeletable && !isHierarchyScoped)
            {
                continue;
            }

            var filter = BuildQueryFilter(clrType);
            modelBuilder.Entity(clrType).HasQueryFilter(filter);
        }
    }

    private LambdaExpression BuildQueryFilter(Type entityType)
    {
        var method = typeof(DotGlassesDbContext)
            .GetMethod(nameof(BuildQueryFilterGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);
        return (LambdaExpression)method.Invoke(this, null)!;
    }

    private static readonly MethodInfo IsAuthenticatedMethod =
        typeof(DotGlassesDbContext).GetMethod(nameof(IsAuthenticated), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo HierarchyPathPrefixMethod =
        typeof(DotGlassesDbContext).GetMethod(nameof(HierarchyPathPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static bool IsAuthenticated(IHttpContextAccessor accessor) =>
        accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    private static string HierarchyPathPrefix(IHttpContextAccessor accessor) =>
        accessor.HttpContext?.User?.FindFirstValue(DotGlassesClaimTypes.HierarchyPath) ?? string.Empty;

    private LambdaExpression BuildQueryFilterGeneric<TEntity>() where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var accessorField = Expression.Field(Expression.Constant(this), nameof(_httpContextAccessor));

        Expression? body = null;

        if (typeof(ISoftDeletable).IsAssignableFrom(typeof(TEntity)))
        {
            var notDeleted = Expression.Equal(
                Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)),
                Expression.Constant(false));
            body = notDeleted;
        }

        if (typeof(IHierarchyScoped).IsAssignableFrom(typeof(TEntity)))
        {
            var hierarchyPathAccess = Expression.Property(parameter, nameof(IHierarchyScoped.HierarchyPath));
            var prefixAccess = Expression.Call(HierarchyPathPrefixMethod, accessorField);
            var startsWithMethod = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
            var startsWithCall = Expression.Call(hierarchyPathAccess, startsWithMethod, prefixAccess);

            var isAuthenticatedAccess = Expression.Call(IsAuthenticatedMethod, accessorField);
            var hierarchyCheck = Expression.AndAlso(isAuthenticatedAccess, startsWithCall);

            body = body is null ? hierarchyCheck : Expression.AndAlso(body, hierarchyCheck);
        }

        return Expression.Lambda(body!, parameter);
    }
}

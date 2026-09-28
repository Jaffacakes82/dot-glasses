using System.Linq.Expressions;
using System.Reflection;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class DotGlassesDbContext(DbContextOptions<DotGlassesDbContext> options, IHttpContextAccessor httpContextAccessor)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IUnitOfWork
{
    // Depends on IHttpContextAccessor (singleton) rather than the scoped ICurrentUserContext
    // directly: DotGlasses.Web registers this DbContext via Aspire's AddAzureNpgsqlDbContext, which
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

    public DbSet<OrganisationNode> OrganisationNodes => Set<OrganisationNode>();
    public DbSet<UserOrgAssignment> UserOrgAssignments => Set<UserOrgAssignment>();
    public DbSet<ReferenceDataItem> ReferenceDataItems => Set<ReferenceDataItem>();
    public DbSet<PresetCatalogue> PresetCatalogues => Set<PresetCatalogue>();
    public DbSet<PresetCatalogueAssignment> PresetCatalogueAssignments => Set<PresetCatalogueAssignment>();
    public DbSet<LensOption> LensOptions => Set<LensOption>();
    public DbSet<LensStrengthCoatingOption> LensStrengthCoatingOptions => Set<LensStrengthCoatingOption>();
    public DbSet<CoatingPairing> CoatingPairings => Set<CoatingPairing>();
    public DbSet<CoatingExclusion> CoatingExclusions => Set<CoatingExclusion>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleCoating> SaleCoatings => Set<SaleCoating>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DotGlassesDbContext).Assembly);

        // Model-level, so it can't live on OrganisationNodeConfiguration's EntityTypeBuilder —
        // see that class for why path segments come from a sequence.
        modelBuilder.HasSequence<int>(OrganisationNodeConfiguration.PathSegmentSequence);

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

    private static readonly MethodInfo ScopePatternsMethod =
        typeof(DotGlassesDbContext).GetMethod(nameof(ScopePatterns), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo LikeMethod =
        typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string)])!;

    private static readonly MethodInfo AnyWithPredicateMethod =
        typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));

    /// <summary>The request's scope as LIKE patterns, one per scope path ("/1/4/" → "/1/4/%").
    /// Hierarchy paths are digits and slashes only (HierarchyPath's invariant), so none of them
    /// carries a LIKE wildcard that would need escaping. Empty — so no row matches — when the
    /// request's access was never loaded: unauthenticated, a background job, or a user with no
    /// assignment.</summary>
    private static string[] ScopePatterns(IHttpContextAccessor accessor) =>
        RequestUserAccess.Get(accessor.HttpContext).ScopePaths.Select(p => p.Value + "%").ToArray();

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
            // patterns.Any(p => EF.Functions.Like(e.HierarchyPath, p)), which Npgsql translates to
            // one predicate — "HierarchyPath" LIKE ANY (@patterns) — over the raw string column
            // (ADR-0004). One predicate, not one per scope path OR'd or UNION'd together, is what
            // makes a row covered by two of a user's assignments count once (ADR-0006). An empty
            // array matches nothing, which is the fail-closed answer for "no scope".
            var hierarchyPathAccess = Expression.Property(parameter, nameof(IHierarchyScoped.HierarchyPath));
            var patterns = Expression.Call(ScopePatternsMethod, accessorField);
            var pattern = Expression.Parameter(typeof(string), "p");
            var like = Expression.Call(
                LikeMethod,
                Expression.Property(null, typeof(EF), nameof(EF.Functions)),
                hierarchyPathAccess,
                pattern);
            var hierarchyCheck = Expression.Call(AnyWithPredicateMethod, patterns, Expression.Lambda<Func<string, bool>>(like, pattern));

            body = body is null ? hierarchyCheck : Expression.AndAlso(body, hierarchyCheck);
        }

        return Expression.Lambda(body!, parameter);
    }
}

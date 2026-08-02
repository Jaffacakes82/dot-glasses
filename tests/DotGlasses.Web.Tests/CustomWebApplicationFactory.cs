using DotGlasses.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotGlasses.Web.Tests;

/// <summary>
/// Swaps the real Postgres DbContext for EF Core InMemory (a fresh database per factory
/// instance) and supplies fixed Jwt settings, so tests don't need a running Postgres/AppHost —
/// see CLAUDE.md: "EF Core InMemory provider is acceptable for now" for test projects.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public readonly string DatabaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:dotglassesdb"] = "Host=localhost;Database=unused-overridden-below",
                ["Jwt:Key"] = "test-signing-key-not-for-production-use-only-1234567890",
                ["Jwt:Issuer"] = "DotGlasses.Web.Tests",
                ["Jwt:Audience"] = "DotGlasses.App.Tests",
                ["Jwt:AccessTokenLifetimeMinutes"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Aspire's AddNpgsqlDbContext registers a pooled context via several interdependent
            // descriptors (DbContextOptions<T>, IDbContextPool<T>, IScopedDbContextLease<T>, T
            // itself) — removing only DbContextOptions<T> leaves the rest dangling and pointed
            // at nothing, so strip every descriptor that mentions DotGlassesDbContext at all.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DotGlassesDbContext)
                    || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(DotGlassesDbContext))))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DotGlassesDbContext>(options => options.UseInMemoryDatabase(DatabaseName));

            // InMemory has no migrations, so it never applies the AspNetRoles HasData seed
            // (RoleSeedConfiguration) the way a real Postgres migration would. Program.cs's
            // DevUserSeeder hosted service assigns the dev admin user to the Admin role and
            // starts before any hook we could register here would run, so the roles need to
            // exist before the host starts — EnsureCreated() is InMemory's equivalent of
            // "apply schema + seed data", done eagerly against a scoped snapshot of the
            // services built so far rather than waiting for the full host.
            using var seedScope = services.BuildServiceProvider().CreateScope();
            seedScope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().Database.EnsureCreated();
        });
    }
}

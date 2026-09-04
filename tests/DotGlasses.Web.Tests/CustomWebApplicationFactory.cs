using DotGlasses.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DotGlasses.Web.Tests;

/// <summary>
/// Boots the real application against a real, containerised Postgres.
///
/// The only thing swapped out is *which* Postgres the app talks to: the connection string
/// Program.cs reads ("ConnectionStrings:dotglassesdb", the name AppHost gives the database
/// resource) is pointed at a throwaway container, and fixed Jwt settings are supplied so tests
/// can mint their own tokens. Everything downstream of that — Aspire's pooled
/// AddNpgsqlDbContext registration, the audit interceptor it attaches, the global query filters,
/// the real migration chain and its seed data — is exactly what runs in production. The
/// previous EF Core InMemory swap could not say that: it replaced the provider outright, so the
/// registration under test was one the application never uses, and no SQL was ever generated.
///
/// State isolation: one container and one database for the whole assembly, deliberately not
/// reset between tests. The API tests address their own rows by client-generated GUID (the
/// offline-sync idempotency key the real Field App uses), so they neither see nor care about a
/// neighbour's rows, and a truncate-between-tests reset would have to preserve the
/// migration-seeded roles, organisation nodes and reference data that the application needs to
/// start at all. Infrastructure.Tests, whose assertions *are* whole-table counts, takes the
/// stricter fresh-database-per-test route instead — see PostgresContainerFixture there.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// The same tag AppHost's .RunAsContainer() Postgres resource pins for local dev — see
    /// PostgresContainerFixture in DotGlasses.Infrastructure.Tests for why it is pinned rather
    /// than floating on :latest. The two assemblies can't share a constant (a test project
    /// referencing another test project would drag its tests along with it), so this is a
    /// deliberate second copy: if the AppHost tag moves, grep for it and change both.
    /// </summary>
    private const string PostgresImage = "postgres:18.3";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(PostgresImage)
        .WithDatabase("dotglasses_web_tests")
        .Build();

    private string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Applied here rather than relying on Program.cs's development-only migrate-on-boot, so
        // the schema exists no matter which environment the test host resolves to.
        var options = new DbContextOptionsBuilder<DotGlassesDbContext>().UseNpgsql(ConnectionString).Options;
        await using var context = new DotGlassesDbContext(options, new NullHttpContextAccessor());
        await context.Database.MigrateAsync();

        NpgsqlConnection.ClearAllPools();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // UseSetting, not ConfigureAppConfiguration, for the connection string alone: Aspire's
        // AddNpgsqlDbContext reads it eagerly while Program.cs is still executing, whereas a
        // ConfigureAppConfiguration source is merged in only just before Build() — late enough
        // for the lazily-bound Jwt options below, far too late for this. UseSetting lands in
        // host configuration, which is in place before any application code runs.
        builder.UseSetting("ConnectionStrings:dotglassesdb", ConnectionString);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-not-for-production-use-only-1234567890",
                ["Jwt:Issuer"] = "DotGlasses.Web.Tests",
                ["Jwt:Audience"] = "DotGlasses.App.Tests",
                ["Jwt:AccessTokenLifetimeMinutes"] = "60",
            });
        });
    }

    /// <summary>
    /// The migration DbContext is built outside the host, before any request exists, so there
    /// is no HttpContext to hand it — and none is needed: the global query filters this would
    /// feed are irrelevant to schema migration.
    /// </summary>
    private sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}

/// <summary>
/// Shared by every API test class, so the container and the host are started once per assembly
/// rather than once per class.
/// </summary>
[CollectionDefinition(Name)]
public class WebApiCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "WebApi";
}

using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DotGlasses.Infrastructure.Tests.Postgres;

/// <summary>
/// One Postgres container for the whole test assembly, plus a per-test database minted from a
/// migrated template.
///
/// Why a container at all: the EF Core InMemory provider implements neither transactions nor
/// SQL string-matching semantics, so neither the atomicity guarantee the conversion services
/// rely on nor the hierarchy query filter's prefix match were ever genuinely exercised.
///
/// Why one container, shared: starting Postgres costs seconds; creating a database inside an
/// already-running one costs milliseconds. Container startup is amortised across the assembly
/// via a collection fixture, and isolation is bought per test instead.
///
/// Why a fresh database per test rather than a truncate-between-tests reset: these tests assert
/// on *whole-table* contents (e.g. "a root-scoped caller sees exactly four widgets"), so residue
/// from a neighbouring test is a false failure. A CREATE DATABASE ... TEMPLATE clone of an
/// already-migrated database is a file copy — cheaper than re-running the migration chain, and
/// it yields genuinely untouched state rather than a best-effort cleanup that a newly added
/// table could silently fall out of.
///
/// Why MigrateAsync and not EnsureCreated: migrations are what production runs, and they carry
/// the HasData seed rows (roles, reference data, organisation nodes). EnsureCreated would build
/// a schema straight from the model that no real environment actually has.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    /// <summary>
    /// Matches the image AppHost's .RunAsContainer() Postgres resource pins for local dev
    /// (Aspire.Hosting.PostgreSQL 13.4.6 resolves docker.io/library/postgres:18.3). Tests and
    /// local dev disagreeing about the server version is exactly the class of surprise this
    /// harness exists to remove, so the tag is pinned rather than left floating on :latest.
    /// </summary>
    public const string PostgresImage = "postgres:18.3";

    private const string TemplateDatabase = "dotglasses_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(PostgresImage).Build();

    private int _databaseCount;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await ExecuteOnMaintenanceDatabaseAsync($"""CREATE DATABASE "{TemplateDatabase}";""");

        await using (var context = CreateContext(ConnectionStringFor(TemplateDatabase)))
        {
            await context.Database.MigrateAsync();
        }

        // CREATE DATABASE ... TEMPLATE refuses to run while anything else is connected to the
        // template, and Npgsql keeps pooled connections open after the DbContext is disposed.
        NpgsqlConnection.ClearAllPools();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Clones the migrated template into a brand-new database and returns its connection
    /// string. The caller gets schema and seed data, and nothing any other test wrote.
    /// </summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"dotglasses_test_{Interlocked.Increment(ref _databaseCount)}";
        await ExecuteOnMaintenanceDatabaseAsync($"""CREATE DATABASE "{name}" TEMPLATE "{TemplateDatabase}";""");
        return ConnectionStringFor(name);
    }

    /// <summary>A DbContext over the given database, with the given interceptors attached.</summary>
    public static DotGlassesDbContext CreateContext(
        string connectionString,
        IHttpContextAccessor? httpContextAccessor = null,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<DotGlassesDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptors)
            .Options;

        return new DotGlassesDbContext(options, httpContextAccessor ?? FakeHttpContextAccessor.Create());
    }

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    private async Task ExecuteOnMaintenanceDatabaseAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Every database-backed test class in this assembly joins this collection, so they share the
/// single container above rather than starting one each.
/// </summary>
[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres";
}

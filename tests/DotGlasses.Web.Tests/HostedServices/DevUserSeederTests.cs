using DotGlasses.Application.Common;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.HostedServices;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DotGlasses.Web.Tests.HostedServices;

/// <summary>
/// An account's access is exactly its UserOrgAssignment rows (ADR-0006), so those rows are all
/// DevUserSeeder seeds besides the account and its role. These pin that it seeds them — the dev
/// Admin with nested assignments (DGI plus a retail point beneath it, so it can also record from
/// the Field App) — whether creating the account fresh, re-running, or catching up an account
/// that already exists in a persisted local database without them.
///
/// Runs against real Postgres, own throwaway container per class (not the shared
/// CustomWebApplicationFactory instance, which never configures DevSeed options and is shared
/// across every other test in the assembly) — the unique index on (UserId, OrgNodeId) and the FK
/// constraints this pins are exactly the kind of thing the InMemory provider can't reproduce.
/// </summary>
public class DevUserSeederTests : IAsyncLifetime
{
    private const string AdminUserName = "seeded-admin@dotglasses.dev";
    private const string AdminPassword = "DevPassw0rd!123";

    private static readonly Guid[] AdminAssignments =
        [OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.KenyaRetailPointId];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.3").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<DotGlassesDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new DotGlassesDbContext(options, new NullHttpContextAccessor());
        await context.Database.MigrateAsync();

        NpgsqlConnection.ClearAllPools();
    }

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task SeedingAFreshAdminAccount_GivesItNestedAssignments()
    {
        await using var host = BuildHost();

        await host.Seeder.StartAsync(CancellationToken.None);

        Assert.Equal(AdminAssignments.Order(), await AdminAssignedOrgIdsAsync());
    }

    [Fact]
    public async Task ReRunningTheSeeder_IsANoOpAndLeavesExactlyTheSameAssignments()
    {
        await using var host = BuildHost();
        await host.Seeder.StartAsync(CancellationToken.None);

        await using var second = BuildHost();
        await second.Seeder.StartAsync(CancellationToken.None);

        Assert.Equal(AdminAssignments.Order(), await AdminAssignedOrgIdsAsync());
    }

    [Fact]
    public async Task ARestartAgainstAPreExistingAccountWithNoAssignmentRows_BackfillsThem()
    {
        // An account already in a persisted local database from before its assignments were
        // seeded: with no rows it has no access at all.
        //
        // Created through UserManager rather than a raw DbContext insert: Identity looks accounts
        // up by NormalizedUserName, which only UserManager.CreateAsync populates — a bare
        // dbContext.Users.Add would leave it null, so DevUserSeeder's own FindByNameAsync would
        // never find this row and would create a second, genuinely duplicate account instead of
        // backfilling this one.
        await using (var setupHost = BuildHost())
        {
            var user = new ApplicationUser { UserName = AdminUserName, Email = AdminUserName, EmailConfirmed = true };
            Assert.True((await setupHost.UserManager.CreateAsync(user, AdminPassword)).Succeeded);
            Assert.True((await setupHost.UserManager.AddToRoleAsync(user, RoleNames.Admin)).Succeeded);
        }

        await using (var verifyBefore = CreateContext())
        {
            Assert.Empty(await verifyBefore.UserOrgAssignments.ToListAsync());
        }

        await using var host = BuildHost();
        await host.Seeder.StartAsync(CancellationToken.None);

        Assert.Equal(AdminAssignments.Order(), await AdminAssignedOrgIdsAsync());

        await using var verifyAfter = CreateContext();
        Assert.Single(await verifyAfter.Users.Where(u => u.UserName == AdminUserName).ToListAsync());
    }

    [Fact]
    public async Task SeedingWithAPasswordThatFailsIdentitysPolicy_ThrowsAndCreatesNoAccount()
    {
        // CreateAsync's IdentityResult used to be discarded, so a policy refusal here would just
        // silently skip seeding — indistinguishable from "DevSeed options weren't set at all".
        // Checking the result turns that into a loud startup failure instead, which is the whole
        // point of the fix: better a hosted service that throws than a dev account nobody notices
        // never got seeded.
        await using var host = Host.Build(_postgres.GetConnectionString(), password: "weak");

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Seeder.StartAsync(CancellationToken.None));

        await using var verify = CreateContext();
        Assert.Empty(await verify.Users.Where(u => u.UserName == AdminUserName).ToListAsync());
    }

    private async Task<List<Guid>> AdminAssignedOrgIdsAsync()
    {
        await using var context = CreateContext();
        var user = await context.Users.SingleAsync(u => u.UserName == AdminUserName);
        return (await context.UserOrgAssignments.Where(a => a.UserId == user.Id).Select(a => a.OrgNodeId).ToListAsync()).Order().ToList();
    }

    private DotGlassesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DotGlassesDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new NullHttpContextAccessor());

    private Host BuildHost() => Host.Build(_postgres.GetConnectionString(), AdminPassword);

    private sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    /// <summary>Mirrors InviteAtomicityTests' InviteHost — a miniature of Program.cs's composition
    /// root (Identity's EF stores over a scoped DbContext, IUserAdminService resolved from the
    /// same scope) so DevUserSeeder resolves its dependencies exactly as it does at real
    /// startup.</summary>
    private sealed class Host : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private readonly IServiceScope _scope;

        private Host(ServiceProvider provider, IServiceScope scope, DevUserSeeder seeder)
        {
            _provider = provider;
            _scope = scope;
            Seeder = seeder;
            UserManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        }

        public DevUserSeeder Seeder { get; }

        public UserManager<ApplicationUser> UserManager { get; }

        public static Host Build(string connectionString, string password)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton<IHttpContextAccessor>(new NullHttpContextAccessor());
            services.AddDbContext<DotGlassesDbContext>(options => options.UseNpgsql(connectionString));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<DotGlassesDbContext>()
                .AddDefaultTokenProviders();
            services.AddScoped<ICurrentUserContext>(_ => new FakeCurrentUserContext());
            services.AddScoped<IUserAdminService, UserAdminService>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var seeder = new DevUserSeeder(
                provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new DevSeedOptions { AdminUserName = AdminUserName, AdminPassword = password }));

            return new Host(provider, scope, seeder);
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }

    /// <summary>Only what UserAdminService.AssignUserToOrgAsync actually reads — it never
    /// consults the caller's scope, so this doesn't need to carry one.</summary>
    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => null;
        public string? UserName => null;
        public IReadOnlyList<DotGlasses.Domain.Common.HierarchyPath> ScopePaths => [];
        public OrganisationLevel? HighestLevel => null;
        public string? Role => null;
        public bool IsSuspended => false;
        public IReadOnlyCollection<string> Roles => [];
        public CurrentLocationCheck CurrentLocation => CurrentLocationCheck.NoLocation;
    }
}

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
/// DevUserSeeder used to set only ApplicationUser.OrgNodeId/HierarchyPath/OrgLevel directly —
/// the fields the JWT/cookie claims and hierarchy scoping read — without ever touching
/// UserOrgAssignments, the table the Field App's location picker (AuthController.MyOrgs) actually
/// reads. That let a dev/nonprod account write records under its assigned org while still showing
/// "no org assigned" in the Field App. These pin the fix: seeding now leaves both in sync, whether
/// creating the account fresh or backfilling one that predates this change (the shape DevUserSeeder
/// itself already handles for OrgNodeId/HierarchyPath — see its own doc comment).
///
/// Runs against real Postgres, own throwaway container per class (not the shared
/// CustomWebApplicationFactory instance, which never configures DevSeed options and is shared
/// across every other test in the assembly) — the unique index on (UserId, OrgNodeId) and the new
/// FK constraints this pins are exactly the kind of thing the InMemory provider can't reproduce.
/// </summary>
public class DevUserSeederTests : IAsyncLifetime
{
    private const string AdminUserName = "seeded-admin@dotglasses.dev";
    private const string AdminPassword = "DevPassw0rd!123";

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
    public async Task SeedingAFreshAdminAccount_AlsoCreatesItsUserOrgAssignmentRow()
    {
        await using var host = BuildHost();

        await host.Seeder.StartAsync(CancellationToken.None);

        await using var verifyContext = CreateContext();
        var user = await verifyContext.Users.SingleAsync(u => u.UserName == AdminUserName);
        Assert.Equal(OrganisationSeedConfiguration.DgiId, user.OrgNodeId);

        var assignment = await verifyContext.UserOrgAssignments.SingleAsync(a => a.UserId == user.Id);
        Assert.Equal(OrganisationSeedConfiguration.DgiId, assignment.OrgNodeId);
    }

    [Fact]
    public async Task ReRunningTheSeeder_IsANoOpAndLeavesExactlyOneAssignmentRow()
    {
        await using var host = BuildHost();
        await host.Seeder.StartAsync(CancellationToken.None);

        await using var second = BuildHost();
        await second.Seeder.StartAsync(CancellationToken.None);

        await using var verifyContext = CreateContext();
        var user = await verifyContext.Users.SingleAsync(u => u.UserName == AdminUserName);
        Assert.Single(await verifyContext.UserOrgAssignments.Where(a => a.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task ARestartAgainstAPreExistingAccountWithNoAssignmentRow_BackfillsOne()
    {
        // The exact shape of the bug report: an account that already has OrgNodeId/HierarchyPath
        // set (so it can already write hierarchy-scoped records and JWT claims look correct) but
        // predates this fix, so UserOrgAssignments has nothing for it — the Field App's location
        // picker shows no org at all even though the account can create records under one.
        //
        // Created through UserManager rather than a raw DbContext insert: Identity looks accounts
        // up by NormalizedUserName, which only UserManager.CreateAsync populates — a bare
        // dbContext.Users.Add would leave it null, so DevUserSeeder's own FindByNameAsync would
        // never find this row and would create a second, genuinely duplicate account instead of
        // backfilling this one.
        await using (var setupHost = BuildHost())
        {
            var user = new ApplicationUser
            {
                UserName = AdminUserName,
                Email = AdminUserName,
                EmailConfirmed = true,
                OrgNodeId = OrganisationSeedConfiguration.DgiId,
                HierarchyPath = OrganisationSeedConfiguration.DgiPath,
                OrgLevel = OrganisationLevel.Dgi,
            };
            Assert.True((await setupHost.UserManager.CreateAsync(user, AdminPassword)).Succeeded);
            Assert.True((await setupHost.UserManager.AddToRoleAsync(user, RoleNames.Admin)).Succeeded);
        }

        await using var verifyBefore = CreateContext();
        Assert.Empty(await verifyBefore.UserOrgAssignments.ToListAsync());

        await using var host = BuildHost();
        await host.Seeder.StartAsync(CancellationToken.None);

        await using var verifyAfter = CreateContext();
        var backfilledUser = await verifyAfter.Users.SingleAsync(u => u.UserName == AdminUserName);
        var assignment = await verifyAfter.UserOrgAssignments.SingleAsync(a => a.UserId == backfilledUser.Id);
        Assert.Equal(OrganisationSeedConfiguration.DgiId, assignment.OrgNodeId);
    }

    private DotGlassesDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<DotGlassesDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new NullHttpContextAccessor());

    private Host BuildHost() => Host.Build(_postgres.GetConnectionString());

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

        public static Host Build(string connectionString)
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
                Options.Create(new DevSeedOptions { AdminUserName = AdminUserName, AdminPassword = AdminPassword }));

            return new Host(provider, scope, seeder);
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }

    /// <summary>Only what UserAdminService.AssignUserToOrgAsync actually reads — it never
    /// consults HierarchyPathPrefix, so this doesn't need to carry a real one.</summary>
    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => null;
        public string? UserName => null;
        public Guid? OrgNodeId => null;
        public string HierarchyPathPrefix => string.Empty;
        public OrganisationLevel? OrgLevel => null;
        public IReadOnlyCollection<string> Roles => [];
    }
}

using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// SuspendAsync/UnsuspendAsync used to make their two Identity writes
/// (SetLockoutEnabledAsync/SetLockoutEndDateAsync) unchecked and outside a transaction — the same
/// shape InviteAtomicityTests pins for InviteAsync, now brought here. Structured the same way:
/// same miniature composition root (Identity's EF stores over one scoped DbContext,
/// UserAdminService resolved from the same scope), same reasoning for why (DotGlassesDbContext
/// *is* the IdentityDbContext, so an explicit transaction opened on it is the only thing that
/// batches UserManager's own internal SaveChanges calls).
///
/// Unlike InviteAsync (which has a third write — the org assignment — that a bad Guid or a
/// duplicate can trip to prove the rollback), Suspend/Unsuspend have nothing outside Identity's
/// own two calls to fail deterministically without a mocking library (none is referenced in this
/// codebase, deliberately). What's pinned here instead is the same thing InviteAtomicityTests'
/// first test pins for InviteAsync: that the writes really do enrol in a transaction opened on
/// this service's own context, plus the observable contract SuspendAsync/UnsuspendAsync promise —
/// both lockout columns land together, unsuspending clears the sentinel, and suspending an
/// already-suspended account is a no-op that leaves it suspended.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SuspendAtomicityTests(PostgresContainerFixture postgres)
{
    private const string SuspendeeEmail = "suspend.me@example.com";
    private const string SuspendeeName = "Suspend Me";

    [Fact]
    public async Task IdentityWrites_EnrolInATransactionOpenedOnTheServicesOwnContext()
    {
        // Same premise as InviteAtomicityTests' equivalent test: a UserManager write made inside a
        // transaction opened on this shared context dies with that transaction when it rolls back.
        // If SuspendAsync's writes didn't share this context's transaction, this probe wouldn't be
        // able to prove anything about them either — so pinning it here directly, against the same
        // UserManager instance UserAdminService holds, is what makes the atomicity claim testable.
        await using var host = await CreateHostAsync();

        const string probeEmail = "shared-context-probe@example.com";

        await using (var transaction = await host.Context.Database.BeginTransactionAsync())
        {
            var probe = new ApplicationUser { UserName = probeEmail, Email = probeEmail };
            Assert.True((await host.UserManager.CreateAsync(probe)).Succeeded);
            await transaction.RollbackAsync();
        }

        await using var verifyContext = CreateContext(host.ConnectionString);
        Assert.Empty(await verifyContext.Users.Where(u => u.Email == probeEmail).ToListAsync());
    }

    [Fact]
    public async Task SuspendingAUser_SetsBothLockoutColumnsTogether()
    {
        await using var host = await CreateHostAsync();
        var userId = await host.CreateUserAsync(SuspendeeEmail, SuspendeeName);

        await host.Service.SuspendAsync(userId);

        await using var verifyContext = CreateContext(host.ConnectionString);
        var user = await verifyContext.Users.SingleAsync(u => u.Id == userId);
        Assert.True(user.LockoutEnabled);
        Assert.True(UserSuspension.IsSuspended(user.LockoutEnd));
    }

    [Fact]
    public async Task SuspendingAnAlreadySuspendedUser_LeavesItSuspended()
    {
        // The spec is explicit that this must stay idempotent — re-suspending must not surface as
        // a refusal or leave the account in a half-suspended state.
        await using var host = await CreateHostAsync();
        var userId = await host.CreateUserAsync(SuspendeeEmail, SuspendeeName);

        await host.Service.SuspendAsync(userId);
        await host.Service.SuspendAsync(userId);

        await using var verifyContext = CreateContext(host.ConnectionString);
        var user = await verifyContext.Users.SingleAsync(u => u.Id == userId);
        Assert.True(UserSuspension.IsSuspended(user.LockoutEnd));
    }

    [Fact]
    public async Task UnsuspendingASuspendedUser_ClearsTheLockoutEndDate()
    {
        await using var host = await CreateHostAsync();
        var userId = await host.CreateUserAsync(SuspendeeEmail, SuspendeeName);
        await host.Service.SuspendAsync(userId);

        await host.Service.UnsuspendAsync(userId);

        await using var verifyContext = CreateContext(host.ConnectionString);
        var user = await verifyContext.Users.SingleAsync(u => u.Id == userId);
        Assert.False(UserSuspension.IsSuspended(user.LockoutEnd));
    }

    [Fact]
    public async Task SuspendingAMissingUser_ThrowsWithoutOpeningATransaction()
    {
        await using var host = await CreateHostAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.SuspendAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UnsuspendingAMissingUser_ThrowsWithoutOpeningATransaction()
    {
        await using var host = await CreateHostAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.UnsuspendAsync(Guid.NewGuid()));
    }

    private static DotGlassesDbContext CreateContext(string connectionString) =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, OrganisationSeedConfiguration.DgiPath));

    private async Task<SuspendHost> CreateHostAsync() => SuspendHost.Build(await postgres.CreateDatabaseAsync());

    /// <summary>Mirrors InviteAtomicityTests' InviteHost — one scoped DbContext, Identity's EF
    /// stores pointed at it, and UserAdminService resolved out of the same scope.</summary>
    private sealed class SuspendHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        private SuspendHost(string connectionString, ServiceProvider provider)
        {
            ConnectionString = connectionString;
            _provider = provider;
            _scope = provider.CreateScope();
            Context = _scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
            UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Service = new UserAdminService(UserManager, Context, new FakeCurrentUserContext
            {
                ScopePaths = [HierarchyPath.Parse(OrganisationSeedConfiguration.DgiPath)],
            });
        }

        public string ConnectionString { get; }

        public DotGlassesDbContext Context { get; }

        public UserManager<ApplicationUser> UserManager { get; }

        public UserAdminService Service { get; }

        public async Task<Guid> CreateUserAsync(string email, string fullName)
        {
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName };
            Assert.True((await UserManager.CreateAsync(user)).Succeeded);
            return user.Id;
        }

        public static SuspendHost Build(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton(FakeHttpContextAccessor.Create(
                isAuthenticated: true, OrganisationSeedConfiguration.DgiPath));
            services.AddDbContext<DotGlassesDbContext>(options => options.UseNpgsql(connectionString));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<DotGlassesDbContext>()
                .AddDefaultTokenProviders();

            return new SuspendHost(connectionString, services.BuildServiceProvider());
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }
}

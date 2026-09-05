using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;
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
/// Inviting a user writes three things — the account, its role, and one row per assigned location
/// — and until now each committed on its own. A failure part-way through left a user with no
/// role, or no location, or both, which User Directory then had to render and an admin had to
/// unpick by hand. These tests pin the replacement guarantee: all three land, or none do.
///
/// They run against real Postgres for the same reason TransactionBehaviourTests does — the
/// in-memory provider implements no transactions and no unique indexes, so "nothing survived the
/// rollback" would have passed without a rollback ever happening.
///
/// The host below is deliberately built the way DotGlasses.Web's Program.cs builds it
/// (AddEntityFrameworkStores&lt;DotGlassesDbContext&gt; over a scoped DbContext) rather than by
/// hand-constructing a UserManager, because the whole atomicity claim rests on Identity and the
/// org-assignment writes sharing one DbContext instance — which the first test asserts outright
/// rather than assuming.
/// </summary>
[Collection(PostgresCollection.Name)]
public class InviteAtomicityTests(PostgresContainerFixture postgres)
{
    private const string InviteeEmail = "new.technician@example.com";
    private const string InviteeName = "New Technician";

    [Fact]
    public async Task IdentityWrites_EnrolInATransactionOpenedOnTheServicesOwnContext()
    {
        // The premise everything else here depends on. DotGlassesDbContext *is* the
        // IdentityDbContext (it derives from IdentityDbContext<ApplicationUser, ...>), and
        // AddEntityFrameworkStores hands UserStore whatever the scope resolved for it — the same
        // instance UserAdminService holds. Were that not so, UserManager's internal SaveChanges
        // calls would run on a second connection outside the transaction and no amount of
        // BeginTransaction would make the invite atomic.
        //
        // Asserted by behaviour rather than by comparing the store's Context property: the
        // registered UserStore's generic arity moves between Identity versions (.NET 10 added a
        // passkey type argument), and what actually matters is not that the reference matches but
        // that a UserManager write lands inside — and dies with — a transaction opened here.
        await using var host = await CreateHostAsync();

        const string probeEmail = "shared-context-probe@example.com";

        await using (var transaction = await host.Context.Database.BeginTransactionAsync())
        {
            var probe = new ApplicationUser
            {
                UserName = probeEmail,
                Email = probeEmail,
                HierarchyPath = OrganisationSeedConfiguration.DgiPath,
            };

            Assert.True((await host.UserManager.CreateAsync(probe)).Succeeded);

            await transaction.RollbackAsync();
        }

        await using var verifyContext = CreateContext(host.ConnectionString);
        Assert.Empty(await verifyContext.Users.Where(u => u.Email == probeEmail).ToListAsync());
    }

    [Fact]
    public async Task InvitingAUser_CreatesTheAccountItsRoleAndEveryLocationTogether()
    {
        await using var host = await CreateHostAsync();

        var result = await host.Service.InviteAsync(
            InviteeEmail,
            InviteeName,
            RoleNames.User,
            [OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailerId]);

        await using var verifyContext = CreateContext(host.ConnectionString);

        var user = await verifyContext.Users.SingleAsync(u => u.Email == InviteeEmail);
        Assert.Equal(result.UserId, user.Id);
        Assert.Equal(InviteeName, user.FullName);
        Assert.Null(user.PasswordHash);

        // The first location listed becomes the primary, denormalized onto the account.
        Assert.Equal(OrganisationSeedConfiguration.KenyaRetailPointId, user.OrgNodeId);
        Assert.Equal(OrganisationSeedConfiguration.KenyaRetailPointPath, user.HierarchyPath);
        Assert.Equal(OrganisationLevel.RetailPoint, user.OrgLevel);

        Assert.Single(await verifyContext.UserRoles.Where(r => r.UserId == user.Id).ToListAsync());

        var assignedOrgIds = await verifyContext.UserOrgAssignments
            .Where(a => a.UserId == user.Id)
            .Select(a => a.OrgNodeId)
            .ToListAsync();
        Assert.Equal(
            [OrganisationSeedConfiguration.KenyaRetailerId, OrganisationSeedConfiguration.KenyaRetailPointId],
            assignedOrgIds.OrderBy(id => id).ToList());
    }

    [Fact]
    public async Task TheSetPasswordTokenFromASuccessfulInvite_IsALiveLink()
    {
        // The invitation email UserDirectoryController sends is built from this token, so
        // "produced on success" has to mean a link that actually works, not just a non-empty
        // string. Redeeming it is the only assertion that proves both.
        await using var host = await CreateHostAsync();

        var result = await host.Service.InviteAsync(
            InviteeEmail, InviteeName, RoleNames.User, [OrganisationSeedConfiguration.KenyaRetailPointId]);

        Assert.NotEmpty(result.PasswordResetToken);
        Assert.Equal(InviteeEmail, result.Email);

        var invitee = await host.UserManager.FindByIdAsync(result.UserId.ToString());
        var reset = await host.UserManager.ResetPasswordAsync(invitee!, result.PasswordResetToken, "DevPassw0rd!");

        Assert.True(reset.Succeeded);
    }

    [Fact]
    public async Task WhenTheRoleAssignmentFails_NoAccountSurvives()
    {
        // The role table is seeded by migration and the invite form only offers RoleNames.All, so
        // this is the shape of a seed/deployment fault rather than something an admin can type.
        // It is also the exact failure the old code was blind to: AddToRoleAsync's result was
        // discarded, so an account with no role at all was reported to the admin as a success.
        await using var host = await CreateHostAsync();

        // Identity's store throws rather than returning a failed IdentityResult when the role
        // row is absent, and InvalidOperationException is this codebase's "missing row or a bug"
        // (CLAUDE.md) — deliberately a 500, not copy for an admin to act on.
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Service.InviteAsync(
            InviteeEmail, InviteeName, "Supervisor", [OrganisationSeedConfiguration.KenyaRetailPointId]));

        await using var verifyContext = CreateContext(host.ConnectionString);
        Assert.Empty(await verifyContext.Users.Where(u => u.Email == InviteeEmail).ToListAsync());
    }

    [Fact]
    public async Task WhenALocationAssignmentFails_NeitherTheAccountNorItsRoleSurvives()
    {
        // Failing on the *last* of the three writes is what the guarantee is really about: the
        // account and the role have already been written by this point, and without the
        // transaction they would both stay. The same location twice trips the unique index on
        // (UserId, OrgNodeId) — a real constraint the in-memory provider never had.
        await using var host = await CreateHostAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => host.Service.InviteAsync(
            InviteeEmail,
            InviteeName,
            RoleNames.User,
            [OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointId]));

        await using var verifyContext = CreateContext(host.ConnectionString);

        var users = await verifyContext.Users.Where(u => u.Email == InviteeEmail).ToListAsync();
        Assert.Empty(users);
        Assert.Empty(await verifyContext.UserRoles.ToListAsync());
        Assert.Empty(await verifyContext.UserOrgAssignments.ToListAsync());

        // No account means no set-password link can point anywhere, which is the other half of
        // "not produced on failure" — the first half being that InviteAsync threw rather than
        // returning the token UserDirectoryController would have emailed.
        Assert.Null(await host.UserManager.FindByEmailAsync(InviteeEmail));
    }

    [Fact]
    public async Task WhenTheEmailIsAlreadyTaken_TheRejectionCarriesReadableCopyAndChangesNothing()
    {
        // InviteUserRequestValidator already checks this, so reaching it means two admins raced.
        // It has to arrive as a business-rule rejection — DomainRuleViolationFilter renders the
        // message verbatim — rather than a raw IdentityResult the screen can't show.
        await using var host = await CreateHostAsync();

        await host.Service.InviteAsync(
            InviteeEmail, InviteeName, RoleNames.User, [OrganisationSeedConfiguration.KenyaRetailPointId]);

        var rejection = await Assert.ThrowsAsync<DomainRuleViolationException>(() => host.Service.InviteAsync(
            InviteeEmail, "Someone Else", RoleNames.Admin, [OrganisationSeedConfiguration.KenyaRetailerId]));

        Assert.Contains("Couldn't create the account", rejection.Message);
        Assert.Contains("already taken", rejection.Message);

        await using var verifyContext = CreateContext(host.ConnectionString);
        var user = await verifyContext.Users.SingleAsync(u => u.Email == InviteeEmail);
        Assert.Equal(InviteeName, user.FullName);
        Assert.Single(await verifyContext.UserOrgAssignments.Where(a => a.UserId == user.Id).ToListAsync());
    }

    private static DotGlassesDbContext CreateContext(string connectionString) =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, OrganisationSeedConfiguration.DgiPath));

    private async Task<InviteHost> CreateHostAsync() => InviteHost.Build(await postgres.CreateDatabaseAsync());

    /// <summary>
    /// A miniature of DotGlasses.Web's composition root: one scoped DbContext, Identity's EF
    /// stores pointed at it, and UserAdminService resolved out of the same scope — so the
    /// UserManager under test is wired exactly as the real one is, not hand-assembled around a
    /// second context that would make atomicity trivially (and falsely) succeed.
    /// </summary>
    private sealed class InviteHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        private InviteHost(string connectionString, ServiceProvider provider)
        {
            ConnectionString = connectionString;
            _provider = provider;
            _scope = provider.CreateScope();
            Context = _scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
            UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Service = new UserAdminService(UserManager, Context, new FakeCurrentUserContext
            {
                HierarchyPathPrefix = OrganisationSeedConfiguration.DgiPath,
            });
        }

        public string ConnectionString { get; }

        public DotGlassesDbContext Context { get; }

        public UserManager<ApplicationUser> UserManager { get; }

        public UserAdminService Service { get; }

        public static InviteHost Build(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            // Ephemeral rather than AddDataProtection(): the set-password token only has to
            // round-trip within one test, and the real provider would write a key ring to the
            // machine running the suite.
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton(FakeHttpContextAccessor.Create(
                isAuthenticated: true, OrganisationSeedConfiguration.DgiPath));
            services.AddDbContext<DotGlassesDbContext>(options => options.UseNpgsql(connectionString));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<DotGlassesDbContext>()
                .AddDefaultTokenProviders();

            return new InviteHost(connectionString, services.BuildServiceProvider());
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
        }
    }
}

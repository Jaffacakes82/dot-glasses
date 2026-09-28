using DotGlasses.Application.Common;
using DotGlasses.Application.Users;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DotGlasses.Web.HostedServices;

/// <summary>
/// [OPEN] placeholder: creates dev users at three org levels so the pipeline — including the
/// 2026-08-04 RBAC policies (OrgLevelRequirement, HierarchyDescendantRequirement) — is
/// exercisable end-to-end without a real provisioning flow. The two agreed role names are
/// seeded via migration now (see Persistence/Configurations/RoleSeedConfiguration.cs), not here —
/// roles are non-secret reference data that needs to exist in every environment, whereas these
/// dev accounts are gated behind DevSeedOptions being configured (never set in production) and
/// their passwords are only ever a local-dev convenience. Real seeding (who gets provisioned, at
/// which org node, by whom) is pending the CEO conversation — do not treat DevSeedOptions as
/// production account provisioning. The "Kenya Manager" account name/constants predate the
/// 2026-08-10 Manager→Admin role collapse (see CLAUDE.md's Access model section) and are left
/// as-is — it's now just an Admin account at Country level, kept under its original username so
/// the existing local Postgres data volume's seeded account is reused rather than duplicated.
///
/// Passwords for all three accounts (Phase 8, 2026-08-12) come from DevSeedOptions/user secrets,
/// not hardcoded constants — KenyaManagerPassword/RetailPointUserPassword used to be literal
/// `const string`s in this file (a public repo), which is the exact gap CLAUDE.md's Phase 8
/// flagged. Usernames stay as `const string` — they're plain identifiers, not secrets, and
/// KenyaManagerUserName in particular is deliberately fixed for the reuse-the-existing-seeded-
/// account reason above. Each of the two below-DGI accounts is now individually gated on its own
/// password being set, not just piggybacking on the top-level Admin check, so setting only some
/// of the three secrets locally seeds only the corresponding accounts rather than erroring.
/// </summary>
public class DevUserSeeder(IServiceScopeFactory scopeFactory, IOptions<DevSeedOptions> devSeedOptions) : IHostedService
{
    public const string KenyaManagerUserName = "kenya-manager@dotglasses.dev";
    public const string RetailPointUserUserName = "retailpoint-user@dotglasses.dev";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userAdminService = scope.ServiceProvider.GetRequiredService<IUserAdminService>();

        var seed = devSeedOptions.Value;
        if (string.IsNullOrEmpty(seed.AdminUserName) || string.IsNullOrEmpty(seed.AdminPassword))
        {
            return;
        }

        // DGI plus a retail point beneath it: nested assignments (the retail point adds nothing to
        // the Admin Portal scope, but makes it somewhere this account can record from the Field
        // App — a DGI assignment alone never is, ADR-0006).
        await CreateOrUpdateAsync(
            userManager, userAdminService,
            new DevSeedAccount(
                seed.AdminUserName, seed.AdminPassword, RoleNames.Admin,
                [OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.KenyaRetailPointId]),
            cancellationToken);

        if (!string.IsNullOrEmpty(seed.KenyaManagerPassword))
        {
            await CreateOrUpdateAsync(
                userManager, userAdminService,
                new DevSeedAccount(KenyaManagerUserName, seed.KenyaManagerPassword, RoleNames.Admin, [OrganisationSeedConfiguration.KenyaId]),
                cancellationToken);
        }

        if (!string.IsNullOrEmpty(seed.RetailPointUserPassword))
        {
            await CreateOrUpdateAsync(
                userManager, userAdminService,
                new DevSeedAccount(RetailPointUserUserName, seed.RetailPointUserPassword, RoleNames.User, [OrganisationSeedConfiguration.KenyaRetailPointId]),
                cancellationToken);
        }
    }

    /// <summary>
    /// Creates the dev account if missing, and in either case makes sure it holds every one of its
    /// org assignments — the local Postgres data volume is deliberately persisted across sessions
    /// (see CLAUDE.md's Deployment section), so an account seeded before an assignment was added
    /// here gets it on the next start. An account's access is exactly its UserOrgAssignment rows
    /// (ADR-0006), so these rows are the whole of the seeding. AssignUserToOrgAsync is a no-op for
    /// a row that already exists, and nothing is ever removed. Password is only set on first
    /// creation, never reset here.
    /// </summary>
    private static async Task CreateOrUpdateAsync(
        UserManager<ApplicationUser> userManager, IUserAdminService userAdminService, DevSeedAccount account, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(account.UserName);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = account.UserName,
                Email = account.UserName,
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(user, account.Password);
            if (!createResult.Succeeded)
            {
                return;
            }

            await userManager.AddToRoleAsync(user, account.Role);
        }

        foreach (var orgNodeId in account.OrgNodeIds)
        {
            await userAdminService.AssignUserToOrgAsync(user.Id, orgNodeId, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed record DevSeedAccount(string UserName, string Password, string Role, IReadOnlyList<Guid> OrgNodeIds);
}

public class DevSeedOptions
{
    public const string SectionName = "DevSeed";

    public string? AdminUserName { get; set; }
    public string? AdminPassword { get; set; }
    public string? KenyaManagerPassword { get; set; }
    public string? RetailPointUserPassword { get; set; }
}

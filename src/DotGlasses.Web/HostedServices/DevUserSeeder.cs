using DotGlasses.Application.Common;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Enums;
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

        await CreateOrUpdateAsync(
            userManager, userAdminService,
            new DevSeedAccount(
                seed.AdminUserName, seed.AdminPassword,
                OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.DgiPath, OrganisationLevel.Dgi,
                RoleNames.Admin),
            cancellationToken);

        if (!string.IsNullOrEmpty(seed.KenyaManagerPassword))
        {
            await CreateOrUpdateAsync(
                userManager, userAdminService,
                new DevSeedAccount(
                    KenyaManagerUserName, seed.KenyaManagerPassword,
                    OrganisationSeedConfiguration.KenyaId, OrganisationSeedConfiguration.KenyaPath, OrganisationLevel.Country,
                    RoleNames.Admin),
                cancellationToken);
        }

        if (!string.IsNullOrEmpty(seed.RetailPointUserPassword))
        {
            await CreateOrUpdateAsync(
                userManager, userAdminService,
                new DevSeedAccount(
                    RetailPointUserUserName, seed.RetailPointUserPassword,
                    OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointPath, OrganisationLevel.RetailPoint,
                    RoleNames.User),
                cancellationToken);
        }
    }

    /// <summary>
    /// Creates the dev account if missing, or backfills its org fields if it already exists —
    /// the local Postgres data volume is deliberately persisted across sessions (see CLAUDE.md's
    /// Deployment section), so an account created before OrgNodeId/OrgLevel existed on
    /// ApplicationUser would otherwise stay stuck with nulls forever and silently fail every
    /// OrgLevelRequirement check. Password is only set on first creation, never reset here.
    ///
    /// Also ensures a matching UserOrgAssignment row exists for the primary org — every other
    /// writer of ApplicationUser.OrgNodeId (UserAdminService.InviteAsync/SwitchActiveOrgAsync)
    /// keeps that table in sync as the "assignable set" behind it, but this seeder used to set
    /// only the denormalized OrgNodeId/HierarchyPath/OrgLevel fields directly. That let a dev
    /// account write records under its assigned org (those fields alone drive the JWT claims and
    /// hierarchy scoping) while the Field App's location picker — which reads UserOrgAssignments,
    /// not ApplicationUser — showed no org at all. AssignUserToOrgAsync is a no-op if the row
    /// already exists, so this also backfills any pre-existing seeded account stuck without one.
    /// </summary>
    private static async Task CreateOrUpdateAsync(
        UserManager<ApplicationUser> userManager, IUserAdminService userAdminService, DevSeedAccount account, CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByNameAsync(account.UserName);
        if (existing is not null)
        {
            if (existing.OrgNodeId != account.OrgNodeId || existing.HierarchyPath != account.HierarchyPath || existing.OrgLevel != account.OrgLevel)
            {
                existing.OrgNodeId = account.OrgNodeId;
                existing.HierarchyPath = account.HierarchyPath;
                existing.OrgLevel = account.OrgLevel;
                await userManager.UpdateAsync(existing);
            }

            await userAdminService.AssignUserToOrgAsync(existing.Id, account.OrgNodeId, cancellationToken);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = account.UserName,
            Email = account.UserName,
            EmailConfirmed = true,
            OrgNodeId = account.OrgNodeId,
            HierarchyPath = account.HierarchyPath,
            OrgLevel = account.OrgLevel,
        };

        var createResult = await userManager.CreateAsync(user, account.Password);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(user, account.Role);
            await userAdminService.AssignUserToOrgAsync(user.Id, account.OrgNodeId, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed record DevSeedAccount(
        string UserName, string Password, Guid OrgNodeId, string HierarchyPath, OrganisationLevel OrgLevel, string Role);
}

public class DevSeedOptions
{
    public const string SectionName = "DevSeed";

    public string? AdminUserName { get; set; }
    public string? AdminPassword { get; set; }
    public string? KenyaManagerPassword { get; set; }
    public string? RetailPointUserPassword { get; set; }
}

using DotGlasses.Application.Common;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DotGlasses.Web.HostedServices;

/// <summary>
/// [OPEN] placeholder: creates dev users at three org levels so the pipeline — including the
/// 2026-08-04 RBAC policies (OrgLevelRequirement, HierarchyDescendantRequirement) — is
/// exercisable end-to-end without a real provisioning flow. The three agreed role names are
/// seeded via migration now (see Persistence/Configurations/RoleSeedConfiguration.cs), not here —
/// roles are non-secret reference data that needs to exist in every environment, whereas these
/// dev accounts are gated behind DevSeedOptions being configured (never set in production) and
/// their passwords are only ever a local-dev convenience. The Manager/User accounts' credentials
/// are fixed (not configurable via DevSeedOptions) since they exist purely to exercise RBAC
/// locally, not for per-environment customization. Real seeding (who gets provisioned, at which
/// org node, by whom) is pending the CEO conversation — do not treat DevSeedOptions as production
/// account provisioning.
/// </summary>
public class DevUserSeeder(IServiceScopeFactory scopeFactory, IOptions<DevSeedOptions> devSeedOptions) : IHostedService
{
    public const string KenyaManagerUserName = "kenya-manager@dotglasses.dev";
    public const string KenyaManagerPassword = "DevPassw0rd!";

    public const string RetailPointUserUserName = "retailpoint-user@dotglasses.dev";
    public const string RetailPointUserPassword = "DevPassw0rd!";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var seed = devSeedOptions.Value;
        if (string.IsNullOrEmpty(seed.AdminUserName) || string.IsNullOrEmpty(seed.AdminPassword))
        {
            return;
        }

        await CreateOrUpdateAsync(
            userManager, seed.AdminUserName, seed.AdminPassword,
            OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.DgiPath, OrganisationLevel.Dgi,
            RoleNames.Admin);

        await CreateOrUpdateAsync(
            userManager, KenyaManagerUserName, KenyaManagerPassword,
            OrganisationSeedConfiguration.KenyaId, OrganisationSeedConfiguration.KenyaPath, OrganisationLevel.Country,
            RoleNames.Manager);

        await CreateOrUpdateAsync(
            userManager, RetailPointUserUserName, RetailPointUserPassword,
            OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointPath, OrganisationLevel.RetailPoint,
            RoleNames.User);
    }

    /// <summary>
    /// Creates the dev account if missing, or backfills its org fields if it already exists —
    /// the local Postgres data volume is deliberately persisted across sessions (see CLAUDE.md's
    /// Deployment section), so an account created before OrgNodeId/OrgLevel existed on
    /// ApplicationUser would otherwise stay stuck with nulls forever and silently fail every
    /// OrgLevelRequirement check. Password is only set on first creation, never reset here.
    /// </summary>
    private static async Task CreateOrUpdateAsync(
        UserManager<ApplicationUser> userManager, string userName, string password,
        Guid orgNodeId, string hierarchyPath, OrganisationLevel orgLevel, string role)
    {
        var existing = await userManager.FindByNameAsync(userName);
        if (existing is not null)
        {
            if (existing.OrgNodeId == orgNodeId && existing.HierarchyPath == hierarchyPath && existing.OrgLevel == orgLevel)
            {
                return;
            }

            existing.OrgNodeId = orgNodeId;
            existing.HierarchyPath = hierarchyPath;
            existing.OrgLevel = orgLevel;
            await userManager.UpdateAsync(existing);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName,
            EmailConfirmed = true,
            OrgNodeId = orgNodeId,
            HierarchyPath = hierarchyPath,
            OrgLevel = orgLevel,
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class DevSeedOptions
{
    public const string SectionName = "DevSeed";

    public string? AdminUserName { get; set; }
    public string? AdminPassword { get; set; }
}

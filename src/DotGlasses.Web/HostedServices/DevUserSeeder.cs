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

        await CreateIfMissingAsync(
            userManager, seed.AdminUserName, seed.AdminPassword,
            OrganisationSeedConfiguration.DgiId, OrganisationSeedConfiguration.DgiPath, OrganisationLevel.Dgi,
            RoleNames.Admin);

        await CreateIfMissingAsync(
            userManager, KenyaManagerUserName, KenyaManagerPassword,
            OrganisationSeedConfiguration.KenyaId, OrganisationSeedConfiguration.KenyaPath, OrganisationLevel.Country,
            RoleNames.Manager);

        await CreateIfMissingAsync(
            userManager, RetailPointUserUserName, RetailPointUserPassword,
            OrganisationSeedConfiguration.KenyaRetailPointId, OrganisationSeedConfiguration.KenyaRetailPointPath, OrganisationLevel.RetailPoint,
            RoleNames.User);
    }

    private static async Task CreateIfMissingAsync(
        UserManager<ApplicationUser> userManager, string userName, string password,
        Guid orgNodeId, string hierarchyPath, OrganisationLevel orgLevel, string role)
    {
        if (await userManager.FindByNameAsync(userName) is not null)
        {
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

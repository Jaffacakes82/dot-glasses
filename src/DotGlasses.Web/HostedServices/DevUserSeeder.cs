using DotGlasses.Application.Common;
using DotGlasses.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DotGlasses.Web.HostedServices;

/// <summary>
/// [OPEN] placeholder: creates one dev admin user so the pipeline is exercisable end-to-end.
/// The three agreed role names are seeded via migration now (see
/// Persistence/Configurations/RoleSeedConfiguration.cs), not here — roles are non-secret
/// reference data that needs to exist in every environment, whereas this dev admin account is
/// gated behind DevSeedOptions being configured (never set in production) and its password is
/// only ever a local-dev convenience. Real seeding (who gets provisioned, at which org node, by
/// whom) is pending the CEO conversation — do not treat DevSeedOptions as production account
/// provisioning.
/// </summary>
public class DevUserSeeder(IServiceScopeFactory scopeFactory, IOptions<DevSeedOptions> devSeedOptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var seed = devSeedOptions.Value;
        if (string.IsNullOrEmpty(seed.AdminUserName) || string.IsNullOrEmpty(seed.AdminPassword))
        {
            return;
        }

        if (await userManager.FindByNameAsync(seed.AdminUserName) is not null)
        {
            return;
        }

        var adminUser = new ApplicationUser
        {
            UserName = seed.AdminUserName,
            Email = seed.AdminUserName,
            EmailConfirmed = true,
            HierarchyPath = "/1/",
        };

        var createResult = await userManager.CreateAsync(adminUser, seed.AdminPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, RoleNames.Admin);
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

using DotGlasses.Application.Common;
using DotGlasses.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DotGlasses.Web.HostedServices;

/// <summary>
/// [OPEN] placeholder: seeds the three agreed role names and one dev admin user so the
/// pipeline is exercisable end-to-end. Real seeding (who gets provisioned, at which org node,
/// by whom) is pending the CEO conversation — do not treat DevSeedOptions as production
/// account provisioning.
/// </summary>
public class RoleAndDevUserSeeder(IServiceScopeFactory scopeFactory, IOptions<DevSeedOptions> devSeedOptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

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

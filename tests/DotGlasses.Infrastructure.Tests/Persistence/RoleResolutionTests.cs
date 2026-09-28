using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
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
/// UserAccessLoader (computing access) and UserAdminService.ListAsync (rendering User Directory)
/// used to each pick "the" role for a multi-role account with their own unordered
/// FirstOrDefault() — GetRolesAsync's own return order is undefined, so the two could disagree.
/// Both now go through RoleNames.Primary (see RoleNamesTests for the rule itself); this pins that
/// UserAdminService.ListAsync actually calls it, against real Postgres, by seeding the roles in
/// the order most likely to expose an unordered pick (reverse-alphabetical) and asserting the
/// deterministic one wins regardless.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RoleResolutionTests(PostgresContainerFixture postgres)
{
    [Fact]
    public async Task ListAsync_ResolvesAMultiRoleAccountsRoleDeterministically()
    {
        var connectionString = await postgres.CreateDatabaseAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        var httpContextAccessor = FakeHttpContextAccessor.Create(isAuthenticated: true, OrganisationSeedConfiguration.DgiPath);
        services.AddSingleton(httpContextAccessor);
        services.AddDbContext<DotGlassesDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<DotGlassesDbContext>()
            .AddDefaultTokenProviders();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        const string email = "multi-role@example.com";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Multi Role" };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);

        // Reverse-alphabetical on purpose: GetRolesAsync's own order tends to follow insertion,
        // so seeding "User" before "Admin" is what would expose an unordered FirstOrDefault()
        // returning "User" instead of the deterministic "Admin".
        Assert.True((await userManager.AddToRoleAsync(user, RoleNames.User)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, RoleNames.Admin)).Succeeded);

        context.UserOrgAssignments.Add(new UserOrgAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrgNodeId = OrganisationSeedConfiguration.KenyaRetailPointId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        var service = new UserAdminService(userManager, context, new FakeCurrentUserContext
        {
            ScopePaths = [HierarchyPath.Parse(OrganisationSeedConfiguration.DgiPath)],
        });

        var rows = await service.ListAsync();
        var row = Assert.Single(rows, r => r.Email == email);
        Assert.Equal(RoleNames.Admin, row.Role);
    }
}

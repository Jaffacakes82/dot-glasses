using DotGlasses.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

/// <summary>
/// Seeds the three agreed role names (RoleNames.All) via HasData so they ship as part of
/// migrations and exist in any environment a migration is applied to, not just wherever
/// RoleAndDevUserSeeder happens to run. IDs are fixed — HasData re-evaluates on every model
/// build and diffs by key, so a changed Id here would read as delete-and-recreate rather than
/// a no-op the next time a migration is added. Don't change them once shipped.
/// </summary>
public class RoleSeedConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    private static readonly Guid AdminRoleId = new("f3b1f4a0-1c4a-4a3e-9c1a-000000000001");
    private static readonly Guid ManagerRoleId = new("f3b1f4a0-1c4a-4a3e-9c1a-000000000002");
    private static readonly Guid UserRoleId = new("f3b1f4a0-1c4a-4a3e-9c1a-000000000003");

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.HasData(
            Role(AdminRoleId, RoleNames.Admin),
            Role(ManagerRoleId, RoleNames.Manager),
            Role(UserRoleId, RoleNames.User));
    }

    private static IdentityRole<Guid> Role(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id.ToString(),
    };
}

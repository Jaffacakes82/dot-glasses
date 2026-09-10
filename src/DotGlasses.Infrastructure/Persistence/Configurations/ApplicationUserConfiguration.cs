using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

/// <summary>
/// Identity's own AddEntityFrameworkStores configures the rest of ApplicationUser — this only
/// adds the FK that was missing on OrgNodeId, the field ApplicationUserClaimsPrincipalFactory
/// copies onto the HierarchyPath/OrgLevel claims. Nullable, so an unassigned account (OrgNodeId
/// null) is still legal; what the constraint now rules out is OrgNodeId pointing at an org that
/// doesn't exist. Restrict rather than Cascade: there's no OrganisationNode delete path today, so
/// this is dormant unless one is ever added, at which point deleting an org a user still points
/// to should fail loudly rather than silently orphan the user's org assignment.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasOne<OrganisationNode>()
            .WithMany()
            .HasForeignKey(x => x.OrgNodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

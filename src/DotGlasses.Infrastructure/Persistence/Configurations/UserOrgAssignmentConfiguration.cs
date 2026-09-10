using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class UserOrgAssignmentConfiguration : IEntityTypeConfiguration<UserOrgAssignment>
{
    public void Configure(EntityTypeBuilder<UserOrgAssignment> builder)
    {
        builder.ToTable("UserOrgAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.OrgNodeId);
        builder.HasIndex(x => new { x.UserId, x.OrgNodeId }).IsUnique();

        // Cascade on the user: an assignment row is meaningless without the user it assigns, and
        // there's no independent lifecycle for it to outlive one. Restrict on the org node,
        // matching ApplicationUserConfiguration/OrganisationNodeConfiguration's own self-reference
        // — no OrganisationNode delete path exists today, so this only ever fires if one is added.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OrganisationNode>()
            .WithMany()
            .HasForeignKey(x => x.OrgNodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

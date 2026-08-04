using DotGlasses.Domain.Entities;
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
    }
}

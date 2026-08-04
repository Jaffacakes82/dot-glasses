using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class OrganisationNodeConfiguration : IEntityTypeConfiguration<OrganisationNode>
{
    public void Configure(EntityTypeBuilder<OrganisationNode> builder)
    {
        builder.ToTable("OrganisationNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Kind).HasMaxLength(200);
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.HasIndex(x => x.HierarchyPath);
        builder.HasIndex(x => x.ParentId);

        builder.HasOne<OrganisationNode>()
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class OrganisationNodeConfiguration : IEntityTypeConfiguration<OrganisationNode>
{
    /// <summary>
    /// The Postgres sequence every new HierarchyPath segment is drawn from. A segment belongs to
    /// its node for good: a deactivated node keeps its path and can be reactivated, so its
    /// segments are never free to reuse. Reading the current max and adding one got that wrong
    /// twice over — it only saw active nodes, and two concurrent creates both read the same max.
    /// Either route put two orgs on one path, which merges their data scopes. A sequence hands
    /// out each value exactly once, whatever else is happening. Gaps (a rolled-back create) are
    /// harmless: segments are identifiers, not counts.
    /// </summary>
    public const string PathSegmentSequence = "OrganisationPathSegments";

    public void Configure(EntityTypeBuilder<OrganisationNode> builder)
    {
        builder.ToTable("OrganisationNodes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        // Deliberately unfiltered by IsDeleted — the backstop behind PathSegmentSequence.
        builder.HasIndex(x => x.HierarchyPath).IsUnique();
        builder.HasIndex(x => x.ParentId);
        builder.HasIndex(x => x.DeactivationGroupId);

        builder.HasOne<OrganisationNode>()
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class CustomOrderConfiguration : IEntityTypeConfiguration<CustomOrder>
{
    public void Configure(EntityTypeBuilder<CustomOrder> builder)
    {
        builder.ToTable("CustomOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.HasIndex(x => x.HierarchyPath);

        // One order per placing record, whatever a retried request does: a Lead places at most
        // one, and a Sale is linked to at most one. The backstop behind the services' own
        // "already recorded" check.
        builder.HasIndex(x => x.LeadId).IsUnique();
        builder.HasIndex(x => x.SaleId).IsUnique();
    }
}

public class LeadCoatingConfiguration : IEntityTypeConfiguration<LeadCoating>
{
    public void Configure(EntityTypeBuilder<LeadCoating> builder)
    {
        builder.ToTable("LeadCoatings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.LeadId);
        builder.HasIndex(x => new { x.LeadId, x.CoatingRefId }).IsUnique();
    }
}

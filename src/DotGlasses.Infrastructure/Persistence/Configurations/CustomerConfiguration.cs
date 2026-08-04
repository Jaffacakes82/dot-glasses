using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.HierarchyPath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
        builder.HasIndex(x => x.HierarchyPath);
        // Supports the fuzzy name+phone match query at the retail point.
        builder.HasIndex(x => new { x.HierarchyPath, x.PhoneNumber });
        builder.HasIndex(x => new { x.HierarchyPath, x.FullName });
    }
}

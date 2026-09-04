using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

public class CoatingPairingConfiguration : IEntityTypeConfiguration<CoatingPairing>
{
    public void Configure(EntityTypeBuilder<CoatingPairing> builder)
    {
        builder.ToTable("CoatingPairings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.TriggerCoatingRefId);
        builder.HasIndex(x => x.PairedCoatingRefId);
        builder.HasIndex(x => new { x.TriggerCoatingRefId, x.PairedCoatingRefId }).IsUnique();
    }
}

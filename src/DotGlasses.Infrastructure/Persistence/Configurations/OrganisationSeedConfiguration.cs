using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotGlasses.Infrastructure.Persistence.Configurations;

/// <summary>
/// Seeds a minimal real org tree (HasData, fixed GUIDs, same pattern as RoleSeedConfiguration) so
/// hierarchy paths, RBAC policies, and DevUserSeeder's test accounts have something real to
/// anchor to.
/// </summary>
public class OrganisationSeedConfiguration : IEntityTypeConfiguration<OrganisationNode>
{
    public static readonly Guid DgiId = new("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid KenyaId = new("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid KenyaRetailerId = new("a0000000-0000-0000-0000-000000000003");
    public static readonly Guid KenyaRetailPointId = new("a0000000-0000-0000-0000-000000000004");

    public const string DgiPath = "/1/";
    public const string KenyaPath = "/1/2/";
    public const string KenyaRetailerPath = "/1/2/3/";
    public const string KenyaRetailPointPath = "/1/2/3/4/";

    public void Configure(EntityTypeBuilder<OrganisationNode> builder)
    {
        var now = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            Node(DgiId, null, "DOT Glasses International", OrganisationLevel.Dgi, null, DgiPath, now),
            Node(KenyaId, DgiId, "Kenya", OrganisationLevel.Country, null, KenyaPath, now),
            Node(KenyaRetailerId, KenyaId, "Kangemi Vision Centre", OrganisationLevel.Intermediate, "Retailer", KenyaRetailerPath, now),
            Node(KenyaRetailPointId, KenyaRetailerId, "Kangemi Vision Centre — Outreach Post", OrganisationLevel.RetailPoint, "Standalone", KenyaRetailPointPath, now));
    }

    private static OrganisationNode Node(
        Guid id, Guid? parentId, string name, OrganisationLevel level, string? kind, string path,
        DateTimeOffset createdAtUtc) => new()
    {
        Id = id,
        ParentId = parentId,
        Name = name,
        Level = level,
        Kind = kind,
        HierarchyPath = path,
        IsTrainingOrg = false,
        CreatedAtUtc = createdAtUtc,
    };
}

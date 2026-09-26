using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// Which lens sets a retail point is offered (ADR-0005). The Field App renders this list as it
/// arrives, so what it contains and the order it arrives in are the product behaviour: every lens
/// set assigned at or above the retail point, alphabetically, never one with no lens powers to sell.
/// </summary>
[Collection(PostgresCollection.Name)]
public class LensSetAvailabilityTests(PostgresContainerFixture postgres)
{
    private static DotGlassesDbContext CreateContext(string connectionString) =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, OrganisationSeedConfiguration.KenyaRetailPointPath));

    private static PresetCatalogueQueryService CreateService(DotGlassesDbContext context) =>
        new(context, new UnscopedReportQueryService(context), new ReferenceDataSnapshotProvider(context));

    private static async Task AddLensSetAsync(string connectionString, string name, Guid assignedTo, bool withLensPowers = true)
    {
        await using var context = CreateContext(connectionString);
        var id = Guid.NewGuid();

        context.PresetCatalogues.Add(new PresetCatalogue { Id = id, Name = name, OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
        context.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = id, OrgNodeId = assignedTo });

        if (withLensPowers)
        {
            context.LensOptions.Add(new LensOption { Id = Guid.NewGuid(), PresetCatalogueId = id, LensStrengthRefId = ReferenceDataSeedConfiguration.LensStrength250Id, SortOrder = 0 });
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task EveryLensSetReachingTheRetailPointIsOffered_Alphabetically()
    {
        // Seeded: "6-Lens Set" and "9-Lens Set", both assigned to Kenya. Added in non-alphabetical
        // order, at two different levels above the retail point.
        var connectionString = await postgres.CreateDatabaseAsync();
        await AddLensSetAsync(connectionString, "Zeta Reading", OrganisationSeedConfiguration.KenyaRetailerId);
        await AddLensSetAsync(connectionString, "alpha Distance", OrganisationSeedConfiguration.KenyaId);

        await using var context = CreateContext(connectionString);
        var offered = await CreateService(context).ListAvailableForCallerAsync(OrganisationSeedConfiguration.KenyaRetailPointPath);

        Assert.Equal(["6-Lens Set", "9-Lens Set", "alpha Distance", "Zeta Reading"], offered.Select(c => c.Name));
    }

    [Fact]
    public async Task ALensSetWithNoLensPowersIsNotOffered()
    {
        // Nothing on it can be sold, so offering it would only lead a technician to an empty picker.
        var connectionString = await postgres.CreateDatabaseAsync();
        await AddLensSetAsync(connectionString, "Still Being Built", OrganisationSeedConfiguration.KenyaId, withLensPowers: false);

        await using var context = CreateContext(connectionString);
        var offered = await CreateService(context).ListAvailableForCallerAsync(OrganisationSeedConfiguration.KenyaRetailPointPath);

        Assert.DoesNotContain(offered, c => c.Name == "Still Being Built");
    }
}

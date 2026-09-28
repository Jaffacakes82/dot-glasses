using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Persistence.Interceptors;
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
            FakeHttpContextAccessor.Create(isAuthenticated: true, OrganisationSeedConfiguration.KenyaRetailPointPath),
            new AuditSaveChangesInterceptor(new FakeCurrentUserContext()));

    private static PresetCatalogueQueryService CreateService(DotGlassesDbContext context) =>
        new(new ReferenceDataSnapshotProvider(context, new UnscopedReportQueryService(context)));

    private static PresetCatalogueAdminService CreateAdminService(DotGlassesDbContext context) =>
        new(context, new ReferenceDataSnapshotProvider(context, new UnscopedReportQueryService(context)));

    private static async Task<Guid> AddLensSetAsync(string connectionString, string name, Guid assignedTo, bool withLensPowers = true)
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
        return id;
    }

    private static async Task<IReadOnlyList<string>> OfferedAtTheRetailPointAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        var offered = await CreateService(context).ListAvailableForCallerAsync(OrganisationSeedConfiguration.KenyaRetailPointPath);
        return offered.Select(c => c.Name).ToList();
    }

    [Fact]
    public async Task ARetiredLensSetIsNotOffered_AndReactivatingItRestoresItWithItsAssignments()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var id = await AddLensSetAsync(connectionString, "Seasonal Readers", OrganisationSeedConfiguration.KenyaRetailerId);

        await using (var context = CreateContext(connectionString))
        {
            await CreateAdminService(context).RetireAsync(id);
        }

        Assert.DoesNotContain("Seasonal Readers", await OfferedAtTheRetailPointAsync(connectionString));

        await using (var context = CreateContext(connectionString))
        {
            await CreateAdminService(context).ReactivateAsync(id);
        }

        // Nothing was re-assigned by hand: retiring kept the assignment to the retailer.
        Assert.Contains("Seasonal Readers", await OfferedAtTheRetailPointAsync(connectionString));
    }

    [Fact]
    public async Task ARetiredLensSetIsListedSeparatelyForReactivation()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var id = await AddLensSetAsync(connectionString, "Seasonal Readers", OrganisationSeedConfiguration.KenyaRetailerId);

        await using var context = CreateContext(connectionString);
        var admin = CreateAdminService(context);
        await admin.RetireAsync(id);

        Assert.DoesNotContain(await admin.ListAsync(), c => c.Id == id);
        Assert.Contains(await admin.ListRetiredAsync(), c => c.Id == id && c.Name == "Seasonal Readers");
    }

    [Fact]
    public async Task ARetiredLensSetStillNamesTheRecordsThatUsedIt()
    {
        // Retiring stops a lens set being offered, not being true: a Lead captured on it still
        // shows its name and lens powers (the Admin Portal's conversion summary resolves them
        // through this snapshot), and the rules see it as present but retired.
        var connectionString = await postgres.CreateDatabaseAsync();
        var id = await AddLensSetAsync(connectionString, "Seasonal Readers", OrganisationSeedConfiguration.KenyaRetailerId);

        await using var context = CreateContext(connectionString);
        await CreateAdminService(context).RetireAsync(id);
        var lensOptionId = context.LensOptions.Single(l => l.PresetCatalogueId == id).Id;

        var snapshot = await new ReferenceDataSnapshotProvider(context, new UnscopedReportQueryService(context)).GetAsync();

        var lensSet = snapshot.FindCatalogue(id);
        Assert.NotNull(lensSet);
        Assert.Equal("Seasonal Readers", lensSet.Name);
        Assert.False(lensSet.IsActive);
        Assert.Equal("+2.50", snapshot.ResolveLensOptionLabel(lensOptionId));
    }

    [Fact]
    public async Task ReactivatingALensSetWhoseNameIsNowTaken_IsRefused()
    {
        // Retiring frees a name (ticket 10); reactivating must not quietly put two active lens
        // sets under it — the name is the only thing a technician tells them apart by.
        var connectionString = await postgres.CreateDatabaseAsync();
        var retired = await AddLensSetAsync(connectionString, "Seasonal Readers", OrganisationSeedConfiguration.KenyaRetailerId);

        await using var context = CreateContext(connectionString);
        var admin = CreateAdminService(context);
        await admin.RetireAsync(retired);
        await AddLensSetAsync(connectionString, "seasonal readers", OrganisationSeedConfiguration.KenyaRetailerId);

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => admin.ReactivateAsync(retired));
        Assert.Contains("Seasonal Readers", ex.Message);
    }

    [Fact]
    public async Task ARetiredLensSetCannotBeAssigned()
    {
        // The assign form doesn't offer one; a hand-built POST gets a sentence, not a silent no-op.
        var connectionString = await postgres.CreateDatabaseAsync();
        var id = await AddLensSetAsync(connectionString, "Seasonal Readers", OrganisationSeedConfiguration.KenyaRetailerId);

        await using var context = CreateContext(connectionString);
        var admin = CreateAdminService(context);
        await admin.RetireAsync(id);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => admin.AssignCatalogueToOrgAsync(id, OrganisationSeedConfiguration.KenyaRetailPointId));
    }

    /// <summary>PresetCatalogueAdminService's own Dgi/Country check on the owning org (defence in
    /// depth, per PresetCatalogue's doc comment) — CataloguesController now only ever calls
    /// CreateAsync with an org from the caller's own validated Dgi/Country assignments (ticket 04),
    /// so this can no longer be reached over HTTP; it stays covered here at the service seam
    /// instead of DomainRuleViolationScreenTests.</summary>
    [Fact]
    public async Task CreatingALensSetOwnedByARetailPoint_IsRefused()
    {
        var connectionString = await postgres.CreateDatabaseAsync();

        await using var context = CreateContext(connectionString);
        var admin = CreateAdminService(context);

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => admin.CreateAsync("Rejected range", null, OrganisationSeedConfiguration.KenyaRetailPointId));
        Assert.Equal("A PresetCatalogue's owning org must be Dgi or Country level.", ex.Message);
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

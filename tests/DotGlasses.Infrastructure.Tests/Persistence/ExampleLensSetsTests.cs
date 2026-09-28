using DotGlasses.Domain.Common;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Infrastructure.Tests.Postgres;
using DotGlasses.Infrastructure.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Tests.Persistence;

/// <summary>
/// The example 6-Lens and 9-Lens sets local dev and the test fixtures get once migrations stop
/// seeding any (ADR-0007): real powers and labels, real coatings, one example pairing, assigned to
/// Kenya as the old seeded sets were — and offered to a Kenyan retail point in the lens-power shape
/// the Field App caches.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ExampleLensSetsTests(PostgresContainerFixture postgres)
{
    private static DotGlassesDbContext CreateContext(string connectionString) =>
        PostgresContainerFixture.CreateContext(
            connectionString,
            FakeHttpContextAccessor.Create(isAuthenticated: true, OrganisationSeedConfiguration.KenyaRetailPointPath));

    [Fact]
    public async Task BothExampleSetsAreOfferedAtAKenyanRetailPoint_InTheLensPowerShape()
    {
        await using var context = CreateContext(await postgres.CreateDatabaseAsync());

        var offered = await new PresetCatalogueQueryService(new ReferenceDataSnapshotProvider(context, new UnscopedReportQueryService(context)))
            .ListAvailableForCallerAsync(OrganisationSeedConfiguration.KenyaRetailPointPath);

        var six = Assert.Single(offered, c => c.Id == ExampleLensSets.SixLensSetId);
        var nine = Assert.Single(offered, c => c.Id == ExampleLensSets.NineLensSetId);
        Assert.Equal(("6-Lens Set", 8), (six.Name, six.LensOptions.Count));
        Assert.Equal(("9-Lens Set", 12), (nine.Name, nine.LensOptions.Count));

        var plus250 = Assert.Single(six.LensOptions, l => l.Id == ExampleLensSets.SixLensPlus250Id);
        Assert.Equal(("+2.50", 2.50m, (decimal?)null, (Guid?)null), (plus250.Label, plus250.Sphere, plus250.Add, plus250.LensTypeRefId));
        Assert.Contains(ReferenceDataSeedConfiguration.CoatingBlueBlockId, plus250.CoatingIds);
        var pairing = Assert.Single(plus250.Pairings);
        Assert.Equal(
            (ReferenceDataSeedConfiguration.CoatingBlueBlockId, ReferenceDataSeedConfiguration.CoatingPhotochromicId),
            (pairing.TriggerCoatingRefId, pairing.PairedCoatingRefId));

        var bifocal = Assert.Single(six.LensOptions, l => l.Label == "Bifocal +2.50");
        Assert.Equal((0.00m, 2.50m, ReferenceDataSeedConfiguration.LensTypeBifocalId), (bifocal.Sphere, bifocal.Add!.Value, bifocal.LensTypeRefId!.Value));
        Assert.Equal(new[] { ReferenceDataSeedConfiguration.CoatingPhotochromicId }, bifocal.CoatingIds);

        // Every lens comes in at least one coating (ADR-0007).
        Assert.All(six.LensOptions.Concat(nine.LensOptions), l => Assert.NotEmpty(l.CoatingIds));
    }

    [Fact]
    public async Task SeedingAgainChangesNothing_AndLeavesAnEditedSetAlone()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = CreateContext(connectionString))
        {
            var six = await context.PresetCatalogues.SingleAsync(c => c.Id == ExampleLensSets.SixLensSetId);
            six.Name = "6-Lens Set (edited)";
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(connectionString))
        {
            await ExampleLensSets.EnsureSeededAsync(context);
        }

        await using (var context = CreateContext(connectionString))
        {
            Assert.Equal("6-Lens Set (edited)", (await context.PresetCatalogues.SingleAsync(c => c.Id == ExampleLensSets.SixLensSetId)).Name);
            Assert.Equal(20, await context.LensOptions.CountAsync());
            Assert.Equal(2, await context.PresetCatalogueAssignments.CountAsync());
        }
    }

    /// <summary>A pairing can never contradict an exclusion (ADR-0001). With pairings on each
    /// lens set lens now (ADR-0007), adding an exclusion checks those instead of the removed
    /// global list.</summary>
    [Fact]
    public async Task AnExclusionContradictingALensSetLensPairing_IsRefused()
    {
        await using var context = CreateContext(await postgres.CreateDatabaseAsync());
        var admin = new ReferenceDataAdminService(context, new ReferenceDataSnapshotProvider(context, new UnscopedReportQueryService(context)));

        var ex = await Assert.ThrowsAsync<DomainRuleViolationException>(() => admin.AddCoatingExclusionAsync(
            ReferenceDataSeedConfiguration.CoatingPhotochromicId, ReferenceDataSeedConfiguration.CoatingBlueBlockId));

        Assert.Equal("Can't add this exclusion — a lens in a lens set pairs these two coatings.", ex.Message);
    }
}

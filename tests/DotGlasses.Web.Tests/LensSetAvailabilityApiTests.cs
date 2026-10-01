using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;
using ContractLensRangeType = DotGlasses.Contracts.Common.LensRangeType;

namespace DotGlasses.Web.Tests;

/// <summary>
/// A record may only name a lens set that reaches where it is made: assigned at or above the
/// caller's retail point (ADR-0005). Before this, "only the lens sets assigned to my scope" held
/// only because the Field App happened to show those — the API accepted any lens set at all.
/// </summary>
[Collection(WebApiCollection.Name)]
public class LensSetAvailabilityApiTests(CustomWebApplicationFactory factory)
{
    private const string CallerOutlet = OrganisationSeedConfiguration.KenyaRetailPointPath;

    private sealed record LensSetFixture(Guid LensSetId, Guid LensOptionId, Guid CoatingId, Guid FrameColourId);

    /// <summary>A lens set assigned only to an org outside the caller's branch of the tree.</summary>
    private LensSetFixture SeedLensSetAssignedElsewhere()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();

        var frameColourId = db.ReferenceDataItems.First(x => x.Category == ReferenceDataCategory.FrameColour && x.IsActive && !x.IsOtherOption).Id;

        var elsewhere = new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = OrganisationSeedConfiguration.DgiId,
            Name = "Elsewhere",
            Level = OrganisationLevel.Country,
            HierarchyPath = $"/1/{Random.Shared.Next(100_000, 999_999)}/",
        };
        var lensSet = new PresetCatalogue { Id = Guid.NewGuid(), Name = $"Elsewhere Readers {Guid.NewGuid():N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId };
        db.OrganisationNodes.Add(elsewhere);
        db.PresetCatalogues.Add(lensSet);
        var (lensId, coatingId) = LensSetTestData.AddSellableLens(db, lensSet.Id);
        db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSet.Id, OrgNodeId = elsewhere.Id });
        db.SaveChanges();

        return new LensSetFixture(lensSet.Id, lensId, coatingId, frameColourId);
    }

    private void AssignTo(Guid lensSetId, Guid orgNodeId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSetId, OrgNodeId = orgNodeId });
        db.SaveChanges();
    }

    private static CreateSaleRequest SaleOn(LensSetFixture fixture) => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        PhoneNumber = "0700111222",
        ConsentGiven = true,
        FrameColourRefId = fixture.FrameColourId,
        FrameCoverage = ContractFrameCoverage.FullFrame,
        LensRangeType = ContractLensRangeType.LensSet,
        PresetCatalogueId = fixture.LensSetId,
        SphereLeft = LensSetTestData.SellableSphere,
        SphereRight = LensSetTestData.SellableSphere,
        PresetPupilDistanceBucket = 2,
        CoatingRefIds = [fixture.CoatingId],
    };

    [Fact]
    public async Task ASaleOnALensSetAssignedOnlyElsewhere_IsRefusedAgainstTheLensSet()
    {
        var fixture = SeedLensSetAssignedElsewhere();

        var response = await CreateAuthenticatedClient().PostAsJsonAsync("api/v1/sales", SaleOn(fixture));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Equal(
            "This lens set isn't available at this retail point — choose another lens range.",
            Assert.Single(errors[nameof(CreateSaleRequest.PresetCatalogueId)]));
    }

    [Fact]
    public async Task ATestOnALensSetAssignedOnlyElsewhere_IsRefusedAgainstTheLensSet()
    {
        var fixture = SeedLensSetAssignedElsewhere();

        var response = await CreateAuthenticatedClient().PostAsJsonAsync("api/v1/tests", new CreateTestRequest
        {
            Id = Guid.NewGuid(),
            LensRangeType = ContractLensRangeType.LensSet,
            PresetCatalogueId = fixture.LensSetId,
            SphereLeft = LensSetTestData.SellableSphere,
            SphereRight = LensSetTestData.SellableSphere,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(CreateTestRequest.PresetCatalogueId), (await ErrorsAsync(response)).Keys);
    }

    [Fact]
    public async Task ALeadOnALensSetAssignedOnlyElsewhere_IsRefusedAgainstTheLensSet()
    {
        var fixture = SeedLensSetAssignedElsewhere();
        Guid reasonNotPurchasedId;
        using (var scope = factory.Services.CreateScope())
        {
            reasonNotPurchasedId = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().ReferenceDataItems
                .First(x => x.Category == ReferenceDataCategory.ReasonNotPurchased && x.IsActive && !x.IsOtherOption).Id;
        }

        var response = await CreateAuthenticatedClient().PostAsJsonAsync("api/v1/leads", new CreateLeadRequest
        {
            Id = Guid.NewGuid(),
            FullName = "Amina Okoro",
            PhoneNumber = "0700111222",
            ReasonNotPurchasedRefId = reasonNotPurchasedId,
            CustomerToldPrice = true,
            LensRangeType = ContractLensRangeType.LensSet,
            PresetCatalogueId = fixture.LensSetId,
            SphereLeft = LensSetTestData.SellableSphere,
            SphereRight = LensSetTestData.SellableSphere,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(CreateLeadRequest.PresetCatalogueId), (await ErrorsAsync(response)).Keys);
    }

    [Fact]
    public async Task TheSameSale_OnceTheLensSetIsAssignedAboveTheCaller_IsAccepted()
    {
        var fixture = SeedLensSetAssignedElsewhere();
        AssignTo(fixture.LensSetId, OrganisationSeedConfiguration.KenyaRetailerId);

        var response = await CreateAuthenticatedClient().PostAsJsonAsync("api/v1/sales", SaleOn(fixture));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ErrorsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(
            property => property.Name,
            IReadOnlyList<string> (property) => property.Value.EnumerateArray().Select(v => v.GetString()!).ToList());
    }

    private HttpClient CreateAuthenticatedClient() => factory.CreateTechnicianClient(CallerOutlet);
}

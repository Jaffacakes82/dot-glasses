using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;
using DomainReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Tests;

/// <summary>
/// On a lens set, a record's coatings follow <em>both</em> chosen lenses (ADR-0007, "Coatings";
/// lens-power ticket 06): only coatings both lenses come in, and every pairing from either lens
/// applies. Asserted end to end on the create endpoints because the rule is only worth what the
/// server enforces — a client that skips the Field App's selector must still be refused, with a
/// field-keyed failure a Failed record can render against the coating control.
/// </summary>
[Collection(WebApiCollection.Name)]
public class LensSetCoatingApiTests(CustomWebApplicationFactory factory)
{
    private const string CallerOutlet = OrganisationSeedConfiguration.KenyaRetailPointPath;

    private static readonly Guid Clear = ReferenceDataSeedConfiguration.CoatingClearId;
    private static readonly Guid BlueBlock = ReferenceDataSeedConfiguration.CoatingBlueBlockId;
    private static readonly Guid Photochromic = ReferenceDataSeedConfiguration.CoatingPhotochromicId;

    /// <summary>
    /// A lens set of its own, assigned above the caller, with three single vision lenses:
    /// +2.50 in Clear, Blue block and Photochromic, pairing Blue block → Photochromic; +3.00 in the
    /// same three with no pairing; and +1.00 in Clear only.
    /// </summary>
    private Guid SeedLensSet()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();

        var lensSet = new PresetCatalogue { Id = Guid.NewGuid(), Name = $"Coating Readers {Guid.NewGuid():N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId };
        db.PresetCatalogues.Add(lensSet);

        var plus250 = AddLens(db, lensSet.Id, "+2.50", 2.50m, Clear, BlueBlock, Photochromic);
        db.LensOptionCoatingPairings.Add(new LensOptionCoatingPairing
        {
            Id = Guid.NewGuid(), LensOptionId = plus250, TriggerCoatingRefId = BlueBlock, PairedCoatingRefId = Photochromic,
        });
        AddLens(db, lensSet.Id, "+3.00", 3.00m, Clear, BlueBlock, Photochromic);
        AddLens(db, lensSet.Id, "+1.00", 1.00m, Clear);

        db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSet.Id, OrgNodeId = OrganisationSeedConfiguration.KenyaRetailerId });
        db.SaveChanges();

        return lensSet.Id;
    }

    private static Guid AddLens(DotGlassesDbContext db, Guid lensSetId, string label, decimal sphere, params Guid[] coatings)
    {
        var lensId = Guid.NewGuid();
        db.LensOptions.Add(new LensOption { Id = lensId, PresetCatalogueId = lensSetId, Label = label, Sphere = sphere });
        foreach (var coating in coatings)
        {
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = lensId, CoatingRefId = coating });
        }

        return lensId;
    }

    private Guid ActiveItem(DomainReferenceDataCategory category)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().ReferenceDataItems
            .First(x => x.Category == category && x.IsActive && !x.IsOtherOption).Id;
    }

    private CreateSaleRequest LensSetSale(Guid lensSetId, decimal sphereLeft, decimal sphereRight, params Guid[] coatings) => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        PhoneNumber = "0700111222",
        ConsentGiven = true,
        FrameColourRefId = ActiveItem(DomainReferenceDataCategory.FrameColour),
        FrameCoverage = ContractFrameCoverage.FullFrame,
        LensRangeType = LensRangeType.LensSet,
        PresetCatalogueId = lensSetId,
        SphereLeft = sphereLeft,
        SphereRight = sphereRight,
        PresetPupilDistanceBucket = 2,
        CoatingRefIds = [.. coatings],
    };

    [Fact]
    public async Task ASaleWithACoatingOnlyOneOfTheTwoLensesComesIn_IsRefused()
    {
        // The left lens (+3.00) comes in Photochromic; the right (+1.00) doesn't.
        var lensSetId = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/sales", LensSetSale(lensSetId, 3.00m, 1.00m, Photochromic));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Equal([nameof(CreateSaleRequest.CoatingRefIds)], errors.Keys);
        Assert.Equal(
            "One of the chosen coatings isn't made on these lenses — choose the coatings again.",
            errors[nameof(CreateSaleRequest.CoatingRefIds)].Single());
    }

    [Fact]
    public async Task ASaleWithATriggerButNotItsPairedCoating_IsRefused_EvenWhenThePairingIsOnTheOtherLens()
    {
        // The pairing is on the right lens (+2.50); the left (+3.00) carries none.
        var lensSetId = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/sales", LensSetSale(lensSetId, 3.00m, 2.50m, BlueBlock));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Equal([nameof(CreateSaleRequest.CoatingRefIds)], errors.Keys);
        Assert.Equal(
            "Photochromic comes with Blue block on these lenses — add Photochromic, or remove Blue block.",
            errors[nameof(CreateSaleRequest.CoatingRefIds)].Single());
    }

    [Fact]
    public async Task ASaleWithATriggerAndItsPairedCoating_IsStored()
    {
        var lensSetId = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/sales", LensSetSale(lensSetId, 3.00m, 2.50m, BlueBlock, Photochromic));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var stored = (await response.Content.ReadFromJsonAsync<SaleDto>())!;
        Assert.Equal(new[] { BlueBlock, Photochromic }.Order(), stored.CoatingRefIds.Order());
    }

    [Fact]
    public async Task ALeadsCoatingPreferenceOutsideWhatBothLensesComeIn_IsRefused()
    {
        var lensSetId = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/leads", new CreateLeadRequest
        {
            Id = Guid.NewGuid(),
            FullName = "Amina Okoro",
            PhoneNumber = "0700111222",
            ReasonNotPurchasedRefId = ActiveItem(DomainReferenceDataCategory.ReasonNotPurchased),
            CustomerToldPrice = true,
            LensRangeType = LensRangeType.LensSet,
            PresetCatalogueId = lensSetId,
            SphereLeft = 3.00m,
            SphereRight = 1.00m,
            CoatingPreferenceRefId = Photochromic,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Equal([nameof(CreateLeadRequest.CoatingPreferenceRefId)], errors.Keys);
    }

    /// <summary>
    /// A lens set whose lenses carry a coating retired since: +1.00 comes only in it, and +2.50 in
    /// Clear and it, with Clear → the retired coating paired. The retired coating is a fresh item
    /// of this test's own, so retiring it touches no other test's reference data.
    /// </summary>
    private (Guid LensSetId, Guid RetiredCoating) SeedLensSetWithARetiredCoating()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();

        var retired = new ReferenceDataItem
        {
            Id = Guid.NewGuid(), Category = DomainReferenceDataCategory.Coating, Code = $"retired-{Guid.NewGuid():N}",
            Label = "Retired tint", SortOrder = 999, IsActive = false,
        };
        db.ReferenceDataItems.Add(retired);

        var lensSet = new PresetCatalogue { Id = Guid.NewGuid(), Name = $"Retired Coating Readers {Guid.NewGuid():N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId };
        db.PresetCatalogues.Add(lensSet);
        AddLens(db, lensSet.Id, "+1.00", 1.00m, retired.Id);
        var plus250 = AddLens(db, lensSet.Id, "+2.50", 2.50m, Clear, retired.Id);
        db.LensOptionCoatingPairings.Add(new LensOptionCoatingPairing
        {
            Id = Guid.NewGuid(), LensOptionId = plus250, TriggerCoatingRefId = Clear, PairedCoatingRefId = retired.Id,
        });
        db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSet.Id, OrgNodeId = OrganisationSeedConfiguration.KenyaRetailerId });
        db.SaveChanges();

        return (lensSet.Id, retired.Id);
    }

    [Fact]
    public async Task ALensWhoseCoatingsAreAllRetired_IsRefusedAgainstTheLens_NotAskedForACoating()
    {
        var (lensSetId, _) = SeedLensSetWithARetiredCoating();

        var response = await Client().PostAsJsonAsync("api/v1/sales", LensSetSale(lensSetId, 1.00m, 2.50m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Equal([nameof(CreateSaleRequest.SphereLeft)], errors.Keys);
        Assert.Equal("This lens has no coatings configured yet, so it can't be sold on a lens set.", errors[nameof(CreateSaleRequest.SphereLeft)].Single());
    }

    [Fact]
    public async Task ARetiredCoating_IsNotOffered_AndItsPairingNoLongerHoldsItsTriggerBack()
    {
        var (lensSetId, retired) = SeedLensSetWithARetiredCoating();

        // What the Field App caches: the lens comes in Clear only, with no pairing.
        var catalogues = await Client().GetFromJsonAsync<List<Contracts.PresetCatalogues.PresetCatalogueDto>>("api/v1/preset-catalogues");
        var lens = catalogues!.Single(c => c.Id == lensSetId).LensOptions.Single(l => l.Sphere == 2.50m);
        Assert.Equal([Clear], lens.CoatingIds);
        Assert.Empty(lens.Pairings);

        // And the server accepts Clear alone on it — the retired paired coating no longer counts.
        var sold = await Client().PostAsJsonAsync("api/v1/sales", LensSetSale(lensSetId, 2.50m, 2.50m, Clear));
        Assert.True(sold.StatusCode == HttpStatusCode.Created, await sold.Content.ReadAsStringAsync());
        Assert.DoesNotContain(retired, (await sold.Content.ReadFromJsonAsync<SaleDto>())!.CoatingRefIds);
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ErrorsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(
            property => property.Name,
            IReadOnlyList<string> (property) => property.Value.EnumerateArray().Select(v => v.GetString()!).ToList());
    }

    private HttpClient Client() => factory.CreateTechnicianClient(CallerOutlet);
}

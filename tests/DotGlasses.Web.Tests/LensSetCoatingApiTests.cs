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
            "Every coating must be configured as available for the chosen lenses (see Lens Sets).",
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

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ErrorsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(
            property => property.Name,
            IReadOnlyList<string> (property) => property.Value.EnumerateArray().Select(v => v.GetString()!).ToList());
    }

    private HttpClient Client() => factory.CreateTechnicianClient(CallerOutlet);
}

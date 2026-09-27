using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DotGlasses.Application.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Auth;
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

        var sellable = db.LensStrengthCoatingOptions.First();
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
        var lensOption = new LensOption { Id = Guid.NewGuid(), PresetCatalogueId = lensSet.Id, LensStrengthRefId = sellable.LensStrengthRefId, SortOrder = 0 };

        db.OrganisationNodes.Add(elsewhere);
        db.PresetCatalogues.Add(lensSet);
        db.LensOptions.Add(lensOption);
        db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSet.Id, OrgNodeId = elsewhere.Id });
        db.SaveChanges();

        return new LensSetFixture(lensSet.Id, lensOption.Id, sellable.CoatingRefId, frameColourId);
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
        LensOptionLeftId = fixture.LensOptionId,
        LensOptionRightId = fixture.LensOptionId,
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
            LensOptionLeftId = fixture.LensOptionId,
            LensOptionRightId = fixture.LensOptionId,
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
            LensRangeType = ContractLensRangeType.LensSet,
            PresetCatalogueId = fixture.LensSetId,
            LensOptionLeftId = fixture.LensOptionId,
            LensOptionRightId = fixture.LensOptionId,
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

    private HttpClient CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        var tokenService = factory.Services.GetRequiredService<IJwtTokenService>();

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, "technician"),
            new(DotGlassesClaimTypes.HierarchyPath, CallerOutlet),
            new(ClaimTypes.Role, RoleNames.User),
        ];

        var (token, _) = tokenService.CreateToken(claims);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

using System.Text.Json;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;

namespace DotGlasses.Web.Tests;

/// <summary>
/// What the Field App caches changes shape with ADR-0007: each lens set lens carries its lens
/// power, lens type, coatings and its own pairings, and global pairings leave the reference-data
/// payload (exclusions stay). Asserted on the raw JSON, because the wire shape is the contract a
/// device's cache holds — a DTO round trip would hide a stale or missing property.
/// </summary>
[Collection(WebApiCollection.Name)]
public class LensSetApiShapeTests(CustomWebApplicationFactory factory)
{
    private HttpClient Technician() => factory.CreateTechnicianClient(OrganisationSeedConfiguration.KenyaRetailPointPath);

    private static JsonElement Lens(JsonElement lensSets, Guid lensSetId, Guid lensId) =>
        lensSets.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == lensSetId)
            .GetProperty("lensOptions").EnumerateArray().Single(l => l.GetProperty("id").GetGuid() == lensId);

    [Fact]
    public async Task EachLensCarriesItsPower_LensType_Coatings_AndPairings()
    {
        using var body = JsonDocument.Parse(await Technician().GetStringAsync("api/v1/preset-catalogues"));

        var plus250 = Lens(body.RootElement, ExampleLensSets.SixLensSetId, ExampleLensSets.SixLensPlus250Id);
        Assert.Equal("+2.50", plus250.GetProperty("label").GetString());
        Assert.Equal(2.50m, plus250.GetProperty("sphere").GetDecimal());
        Assert.Equal(JsonValueKind.Null, plus250.GetProperty("cylinder").ValueKind);
        Assert.Equal(JsonValueKind.Null, plus250.GetProperty("axis").ValueKind);
        Assert.Equal(JsonValueKind.Null, plus250.GetProperty("add").ValueKind);
        Assert.Equal(JsonValueKind.Null, plus250.GetProperty("lensTypeRefId").ValueKind);
        Assert.Equal(
            [ReferenceDataSeedConfiguration.CoatingPhotochromicId, ReferenceDataSeedConfiguration.CoatingClearId, ReferenceDataSeedConfiguration.CoatingBlueBlockId],
            plus250.GetProperty("coatingIds").EnumerateArray().Select(x => x.GetGuid()).ToArray());
        var pairing = Assert.Single(plus250.GetProperty("pairings").EnumerateArray().ToList());
        Assert.Equal(ReferenceDataSeedConfiguration.CoatingBlueBlockId, pairing.GetProperty("triggerCoatingRefId").GetGuid());
        Assert.Equal(ReferenceDataSeedConfiguration.CoatingPhotochromicId, pairing.GetProperty("pairedCoatingRefId").GetGuid());

        // The old shape's properties are gone rather than left behind empty.
        Assert.False(plus250.TryGetProperty("sortOrder", out _));
        Assert.False(plus250.TryGetProperty("availableCoatingIds", out _));

        var bifocal = body.RootElement.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == ExampleLensSets.SixLensSetId)
            .GetProperty("lensOptions").EnumerateArray().Single(l => l.GetProperty("label").GetString() == "Bifocal +2.50");
        Assert.Equal(0.00m, bifocal.GetProperty("sphere").GetDecimal());
        Assert.Equal(2.50m, bifocal.GetProperty("add").GetDecimal());
        Assert.Equal(ReferenceDataSeedConfiguration.LensTypeBifocalId, bifocal.GetProperty("lensTypeRefId").GetGuid());
    }

    [Fact]
    public async Task TheCoatingRulesPayloadCarriesExclusionsOnly()
    {
        using var body = JsonDocument.Parse(await Technician().GetStringAsync("api/v1/reference-data/coating-rules"));

        Assert.Equal(JsonValueKind.Array, body.RootElement.GetProperty("exclusions").ValueKind);
        Assert.False(body.RootElement.TryGetProperty("pairings", out _));
    }

    [Fact]
    public async Task NoReferenceDataItemIsInTheRetiredLensStrengthCategory()
    {
        using var body = JsonDocument.Parse(await Technician().GetStringAsync("api/v1/reference-data"));

        Assert.DoesNotContain(body.RootElement.EnumerateArray(), item => item.GetProperty("category").GetInt32() == 6);
    }
}

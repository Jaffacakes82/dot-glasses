using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;
using DomainReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Tests;

/// <summary>
/// A record made from a lens set stores each eye's lens power and one lens type — the same fields
/// a Custom prescription fills — and no pointer to a lens (ADR-0007, lens-power ticket 05). The
/// server accepts it only when each eye's power, with the lens type, is a lens in the chosen set.
/// Asserted end to end because the create requests lost two properties: a device can still post
/// the old shape, and it has to come back as a field-keyed rejection the Failed records screen can
/// render against the lens controls.
/// </summary>
[Collection(WebApiCollection.Name)]
public class LensSetRecordApiTests(CustomWebApplicationFactory factory)
{
    private const string CallerOutlet = OrganisationSeedConfiguration.KenyaRetailPointPath;

    private sealed record LensSetFixture(Guid LensSetId, Guid CoatingId);

    /// <summary>A lens set of its own (so no other test's edits to the example sets can reach it),
    /// assigned above the caller: single vision +2.50 and +3.00, and a Bifocal 0.00 / add +2.50.</summary>
    private LensSetFixture SeedLensSet()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();

        var lensSet = new PresetCatalogue { Id = Guid.NewGuid(), Name = $"Record Readers {Guid.NewGuid():N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId };
        db.PresetCatalogues.Add(lensSet);
        var (_, coatingId) = LensSetTestData.AddSellableLens(db, lensSet.Id);
        LensSetTestData.AddSellableLens(db, lensSet.Id, "+3.00", 3.00m);

        var bifocalId = Guid.NewGuid();
        db.LensOptions.Add(new LensOption
        {
            Id = bifocalId, PresetCatalogueId = lensSet.Id, Label = "Bifocal +2.50",
            Sphere = 0.00m, Add = 2.50m, LensTypeRefId = ReferenceDataSeedConfiguration.LensTypeBifocalId,
        });
        db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = bifocalId, CoatingRefId = coatingId });

        db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSet.Id, OrgNodeId = OrganisationSeedConfiguration.KenyaRetailerId });
        db.SaveChanges();

        return new LensSetFixture(lensSet.Id, coatingId);
    }

    private Guid ActiveItem(DomainReferenceDataCategory category)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().ReferenceDataItems
            .First(x => x.Category == category && x.IsActive && !x.IsOtherOption).Id;
    }

    private CreateSaleRequest Sale(Action<CreateSaleRequest> adjust)
    {
        var sale = new CreateSaleRequest
        {
            Id = Guid.NewGuid(),
            FullName = "Amina Okoro",
            PhoneNumber = "0700111222",
            ConsentGiven = true,
            FrameColourRefId = ActiveItem(DomainReferenceDataCategory.FrameColour),
            FrameCoverage = ContractFrameCoverage.FullFrame,
            CoatingRefIds = [ReferenceDataSeedConfiguration.CoatingClearId],
        };
        adjust(sale);
        return sale;
    }

    [Fact]
    public async Task ALensSetTest_StoresEachEyesPower_AndNoLensType()
    {
        var fixture = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/tests", new CreateTestRequest
        {
            Id = Guid.NewGuid(),
            Gender = Gender.Female,
            Outcome = TestOutcome.NeedsGlasses,
            LensRangeType = LensRangeType.LensSet,
            PresetCatalogueId = fixture.LensSetId,
            SphereLeft = 2.50m,
            SphereRight = 3.00m,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = (await response.Content.ReadFromJsonAsync<TestDto>())!;
        Assert.Equal((fixture.LensSetId, 2.50m, 3.00m), (stored.PresetCatalogueId!.Value, stored.SphereLeft!.Value, stored.SphereRight!.Value));
        Assert.Null(stored.LensTypeRefId);

        var row = Row(db => db.Tests.IgnoreQueryFilters().Single(t => t.Id == stored.Id));
        Assert.Equal((2.50m, 3.00m), (row.SphereLeft!.Value, row.SphereRight!.Value));
    }

    [Fact]
    public async Task ALensSetLead_StoresABifocalPairsPowers_AndItsLensType()
    {
        var fixture = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/leads", new CreateLeadRequest
        {
            Id = Guid.NewGuid(),
            FullName = "Amina Okoro",
            PhoneNumber = "0700111222",
            ReasonNotPurchasedRefId = ActiveItem(DomainReferenceDataCategory.ReasonNotPurchased),
            CustomerToldPrice = true,
            LensRangeType = LensRangeType.LensSet,
            PresetCatalogueId = fixture.LensSetId,
            SphereLeft = 0.00m,
            AddLeft = 2.50m,
            SphereRight = 0.00m,
            AddRight = 2.50m,
            LensTypeRefId = ReferenceDataSeedConfiguration.LensTypeBifocalId,
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var stored = (await response.Content.ReadFromJsonAsync<LeadDto>())!;
        Assert.Equal((0.00m, 2.50m, 0.00m, 2.50m), (stored.SphereLeft!.Value, stored.AddLeft!.Value, stored.SphereRight!.Value, stored.AddRight!.Value));
        Assert.Equal(ReferenceDataSeedConfiguration.LensTypeBifocalId, stored.LensTypeRefId);
    }

    [Fact]
    public async Task ALensSetSale_StoresEachEyesPower()
    {
        var fixture = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/sales", Sale(sale =>
        {
            sale.LensRangeType = LensRangeType.LensSet;
            sale.PresetCatalogueId = fixture.LensSetId;
            sale.SphereLeft = 3.00m;
            sale.SphereRight = 2.50m;
            sale.PresetPupilDistanceBucket = 2;
            sale.CoatingRefIds = [fixture.CoatingId];
        }));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var stored = (await response.Content.ReadFromJsonAsync<SaleDto>())!;
        Assert.Equal((LensRangeType.LensSet, 3.00m, 2.50m), (stored.LensRangeType, stored.SphereLeft!.Value, stored.SphereRight!.Value));

        var row = Row(db => db.Sales.IgnoreQueryFilters().Single(s => s.Id == stored.Id));
        Assert.Equal((fixture.LensSetId, 3.00m, 2.50m), (row.PresetCatalogueId!.Value, row.SphereLeft!.Value, row.SphereRight!.Value));
    }

    [Fact]
    public async Task ACustomSale_StoresEachEyesPower()
    {
        var response = await Client().PostAsJsonAsync("api/v1/sales", Sale(sale =>
        {
            sale.LensRangeType = LensRangeType.Custom;
            sale.SphereLeft = -1.25m;
            sale.CylinderLeft = -0.50m;
            sale.AxisLeft = 90m;
            sale.SphereRight = -1.50m;
            sale.PupilDistanceMm = 62m;
        }));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var stored = (await response.Content.ReadFromJsonAsync<SaleDto>())!;
        Assert.Equal((-1.25m, -0.50m, 90m, -1.50m), (stored.SphereLeft!.Value, stored.CylinderLeft!.Value, stored.AxisLeft!.Value, stored.SphereRight!.Value));
        Assert.Null(stored.PresetCatalogueId);
    }

    [Fact]
    public async Task ACustomSaleSpellingNoneAsZero_StoresTheSameShapeAsALensSetLens()
    {
        // A Custom +3.00 posted with a 0.00 cylinder and a 0.00 add is stored with neither, exactly
        // as the lens set's +3.00 is (LensPowerRules.Normalise on write) — so the two can't be told
        // apart by how a client happened to spell "none".
        var fixture = SeedLensSet();

        var custom = await Client().PostAsJsonAsync("api/v1/sales", Sale(sale =>
        {
            sale.LensRangeType = LensRangeType.Custom;
            sale.SphereLeft = 3.00m;
            sale.CylinderLeft = 0.00m;
            sale.AddLeft = 0.00m;
            sale.SphereRight = 3.00m;
            sale.CylinderRight = 0.00m;
            sale.AddRight = 0.00m;
            sale.PupilDistanceMm = 62m;
        }));
        var lensSet = await Client().PostAsJsonAsync("api/v1/sales", Sale(sale =>
        {
            sale.LensRangeType = LensRangeType.LensSet;
            sale.PresetCatalogueId = fixture.LensSetId;
            sale.SphereLeft = 3.00m;
            sale.SphereRight = 3.00m;
            sale.PresetPupilDistanceBucket = 2;
            sale.CoatingRefIds = [fixture.CoatingId];
        }));

        Assert.True(custom.StatusCode == HttpStatusCode.Created, await custom.Content.ReadAsStringAsync());
        Assert.True(lensSet.StatusCode == HttpStatusCode.Created, await lensSet.Content.ReadAsStringAsync());
        var customId = (await custom.Content.ReadFromJsonAsync<SaleDto>())!.Id;
        var lensSetId = (await lensSet.Content.ReadFromJsonAsync<SaleDto>())!.Id;

        (decimal? Sphere, decimal? Cylinder, decimal? Axis, decimal? Add, decimal? SphereR, decimal? CylinderR, decimal? AxisR, decimal? AddR) Powers(Guid id) =>
            Row(db => db.Sales.IgnoreQueryFilters().Where(s => s.Id == id)
                .Select(s => new { s.SphereLeft, s.CylinderLeft, s.AxisLeft, s.AddLeft, s.SphereRight, s.CylinderRight, s.AxisRight, s.AddRight })
                .AsEnumerable()
                .Select(s => (s.SphereLeft, s.CylinderLeft, s.AxisLeft, s.AddLeft, s.SphereRight, s.CylinderRight, s.AxisRight, s.AddRight))
                .Single());

        var customPowers = Powers(customId);
        Assert.Equal(((decimal?)3.00m, (decimal?)3.00m), (customPowers.Sphere, customPowers.SphereR));
        Assert.All(
            new[] { customPowers.Cylinder, customPowers.Axis, customPowers.Add, customPowers.CylinderR, customPowers.AxisR, customPowers.AddR },
            value => Assert.Null(value));
        Assert.Equal(Powers(lensSetId), customPowers);
    }

    [Fact]
    public async Task AnOverLongLensTypeText_OnALead_IsAKeyedRefusal_NotADatabaseError()
    {
        var response = await Client().PostAsJsonAsync("api/v1/leads", new CreateLeadRequest
        {
            Id = Guid.NewGuid(),
            FullName = "Amina Okoro",
            PhoneNumber = "0700111222",
            ReasonNotPurchasedRefId = ActiveItem(DomainReferenceDataCategory.ReasonNotPurchased),
            CustomerToldPrice = true,
            LensRangeType = LensRangeType.Custom,
            SphereLeft = 1.00m,
            AddLeft = 2.00m,
            SphereRight = 1.00m,
            LensTypeRefId = ReferenceDataSeedConfiguration.LensTypeBifocalId,
            LensTypeOtherText = new string('a', 201),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertKeys([nameof(CreateLeadRequest.LensTypeOtherText)], await ErrorsAsync(response));
    }

    /// <summary>
    /// A device can still hold a lens-set record queued before this release: the outbox posts the
    /// stored JSON as-is, so it names its lenses by id and sends no powers. The binder ignores the
    /// unknown names, the record reaches the rules with no spheres, and it is refused against each
    /// eye's lens — which lands it on Failed records against the lens controls (accepted, spec).
    /// </summary>
    [Fact]
    public async Task AnOldShapeRequestNamingLensesById_IsRefusedAgainstEachEye()
    {
        var fixture = SeedLensSet();
        var payload = $$"""
            {
              "id": "{{Guid.NewGuid()}}",
              "gender": {{(int)Gender.Female}},
              "outcome": {{(int)TestOutcome.NeedsGlasses}},
              "lensRangeType": {{(int)LensRangeType.LensSet}},
              "presetCatalogueId": "{{fixture.LensSetId}}",
              "lensOptionLeftId": "{{Guid.NewGuid()}}",
              "lensOptionRightId": "{{Guid.NewGuid()}}"
            }
            """;

        var response = await Client().PostAsync("api/v1/tests", new StringContent(payload, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        AssertKeys([nameof(CreateTestRequest.SphereLeft), nameof(CreateTestRequest.SphereRight)], errors);
        Assert.Equal("Choose a lens for the left eye.", errors[nameof(CreateTestRequest.SphereLeft)].Single());
        Assert.Equal("Choose a lens for the right eye.", errors[nameof(CreateTestRequest.SphereRight)].Single());
    }

    [Fact]
    public async Task ALensSetPowerMatchingNoLensInTheSet_IsRefusedAgainstThatEye()
    {
        var fixture = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/sales", Sale(sale =>
        {
            sale.LensRangeType = LensRangeType.LensSet;
            sale.PresetCatalogueId = fixture.LensSetId;
            sale.SphereLeft = 2.75m; // a real power, but not a lens this set holds
            sale.SphereRight = 2.50m;
            sale.PresetPupilDistanceBucket = 2;
            sale.CoatingRefIds = [fixture.CoatingId];
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        AssertKeys([nameof(CreateSaleRequest.SphereLeft)], errors);
        Assert.Equal("No lens in this lens set has the left eye's lens power — choose a lens.", errors[nameof(CreateSaleRequest.SphereLeft)].Single());
    }

    [Fact]
    public async Task AMixedPair_IsRefusedAgainstTheRightEye()
    {
        var fixture = SeedLensSet();

        var response = await Client().PostAsJsonAsync("api/v1/tests", new CreateTestRequest
        {
            Id = Guid.NewGuid(),
            Gender = Gender.Female,
            Outcome = TestOutcome.NeedsGlasses,
            LensRangeType = LensRangeType.LensSet,
            PresetCatalogueId = fixture.LensSetId,
            SphereLeft = 0.00m,
            AddLeft = 2.50m,
            LensTypeRefId = ReferenceDataSeedConfiguration.LensTypeBifocalId,
            SphereRight = 2.50m,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        AssertKeys([nameof(CreateTestRequest.SphereRight)], errors);
    }

    private T Row<T>(Func<DotGlassesDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>());
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ErrorsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(
            property => property.Name,
            IReadOnlyList<string> (property) => property.Value.EnumerateArray().Select(v => v.GetString()!).ToList());
    }

    /// <summary>Sorted, because the body's key order is ModelStateDictionary's own.</summary>
    private static void AssertKeys(IEnumerable<string> expected, IReadOnlyDictionary<string, IReadOnlyList<string>> errors) =>
        Assert.Equal(expected.OrderBy(k => k, StringComparer.Ordinal), errors.Keys.OrderBy(k => k, StringComparer.Ordinal));

    private HttpClient Client() => factory.CreateTechnicianClient(CallerOutlet);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.DependencyInjection;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;
using DomainReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Tests;

/// <summary>
/// What the recording forms may leave out and what a Lead must answer (Spec D): the referral
/// location is optional on all three records, and a Lead says whether the customer was told the
/// price — either answer saves, no answer is a field-keyed rejection the Failed records screen can
/// put under the buttons.
/// </summary>
[Collection(WebApiCollection.Name)]
public class RecordingFormApiTests(CustomWebApplicationFactory factory)
{
    private const string CallerOutlet = OrganisationSeedConfiguration.KenyaRetailPointPath;

    private HttpClient Client() => factory.CreateTechnicianClient(CallerOutlet);

    private Guid ActiveItem(DomainReferenceDataCategory category)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>().ReferenceDataItems
            .First(x => x.Category == category && x.IsActive && !x.IsOtherOption).Id;
    }

    private CreateLeadRequest Lead(bool? customerToldPrice) => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        PhoneNumber = "0700111222",
        Gender = Gender.Female,
        ReasonNotPurchasedRefId = ActiveItem(DomainReferenceDataCategory.ReasonNotPurchased),
        CustomerToldPrice = customerToldPrice,
    };

    [Fact]
    public async Task ATestReferredElsewhere_SavesWithNoReferralLocation()
    {
        var response = await Client().PostAsJsonAsync("api/v1/tests", new CreateTestRequest
        {
            Id = Guid.NewGuid(),
            Gender = Gender.Female,
            Outcome = TestOutcome.NoGlassesNeeded,
            ReferredOrTreated = true,
            ReferralReasonRefId = ActiveItem(DomainReferenceDataCategory.ReferralReason),
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.Null((await response.Content.ReadFromJsonAsync<TestDto>())!.ReferralLocationFreeText);
    }

    [Fact]
    public async Task ALeadReferredElsewhere_SavesWithNoReferralLocation()
    {
        var lead = Lead(customerToldPrice: true);
        lead.ReferredOrTreated = true;
        lead.ReferralReasonRefId = ActiveItem(DomainReferenceDataCategory.ReferralReason);

        var response = await Client().PostAsJsonAsync("api/v1/leads", lead);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ASaleReferredElsewhere_SavesWithNoReferralLocation()
    {
        var response = await Client().PostAsJsonAsync("api/v1/sales", new CreateSaleRequest
        {
            Id = Guid.NewGuid(),
            FullName = "Amina Okoro",
            ConsentGiven = true,
            ReferredOrTreated = true,
            ReferralReasonRefId = ActiveItem(DomainReferenceDataCategory.ReferralReason),
            LensRangeType = LensRangeType.Custom,
            SphereLeft = 1.00m,
            SphereRight = 1.00m,
            PupilDistanceMm = 62m,
            FrameColourRefId = ActiveItem(DomainReferenceDataCategory.FrameColour),
            FrameCoverage = ContractFrameCoverage.FullFrame,
            CoatingRefIds = [ReferenceDataSeedConfiguration.CoatingClearId],
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ALeadWithNoPriceAnswer_IsRefusedAgainstThatQuestion()
    {
        var response = await Client().PostAsJsonAsync("api/v1/leads", Lead(customerToldPrice: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var error = Assert.Single(body.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Equal("CustomerToldPrice", error.Name);
        Assert.Equal(
            "Choose Yes or No for \"Has the customer been told the price?\".",
            error.Value.EnumerateArray().Single().GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ALeadWithEitherPriceAnswer_IsCreated_AndReturnsIt(bool told)
    {
        var client = Client();
        var lead = Lead(told);

        var response = await client.PostAsJsonAsync("api/v1/leads", lead);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.Equal(told, (await response.Content.ReadFromJsonAsync<LeadDto>())!.CustomerToldPrice);

        var read = await client.GetFromJsonAsync<LeadDto>($"api/v1/leads/{lead.Id}");
        Assert.Equal(told, read!.CustomerToldPrice);
    }
}

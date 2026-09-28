using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;

namespace DotGlasses.Web.Tests;

/// <summary>
/// The contract a rejected consultation create actually puts on the wire, asserted end to end
/// rather than against the rule module in isolation.
///
/// Ticket 12 deleted the three FluentValidation validators and moved every rule behind them into
/// DotGlasses.Rules, which the three create endpoints now call directly. Nothing about the
/// response was supposed to change, and this is where that is checked: the keys are the request
/// DTO's own property names (which is what lets the Field App's FormErrors bag map a server
/// rejection onto the right control with no translation table), and the messages are the exact
/// strings clients already received — FluentValidation's generated copy included, spaced display
/// names and interpolated lengths and all.
///
/// Keys are asserted exhaustively rather than by Contains: a rule that quietly stops firing is as
/// much a regression as one that starts, and only an exact set catches the first. They are
/// compared as a sorted set because the body's key order is ModelStateDictionary's, not the order
/// the rules reported in — that was already true of the validators, so it is not something this
/// refactor could preserve or break.
/// </summary>
[Collection(WebApiCollection.Name)]
public class ConsultationValidationApiTests(CustomWebApplicationFactory factory)
{
    private const string CallerOutlet = "/1/2/3/4/";

    [Fact]
    public async Task ATestFailingScalarAndReferenceDataRules_ReportsEachAgainstItsOwnField()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/tests", new CreateTestRequest
        {
            Id = Guid.Empty,
            Gender = (Gender)99,
            AgeYears = 999,
            OccupationRefId = Guid.NewGuid(),
            OccupationOtherText = new string('a', 201),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["Id", "Gender", "AgeYears", "OccupationOtherText", "OccupationRefId"], errors);
        Assert.Equal("'Id' must not be empty.", errors["Id"].Single());
        Assert.Equal("'Gender' has a range of values which does not include '99'.", errors["Gender"].Single());
        Assert.Equal("'Age Years' must be between 0 and 120. You entered 999.", errors["AgeYears"].Single());
        Assert.Equal(
            "The length of 'Occupation Other Text' must be 200 characters or fewer. You entered 201 characters.",
            errors["OccupationOtherText"].Single());
        Assert.Equal(
            "OccupationRefId must reference an existing, active Occupation reference-data item.",
            errors["OccupationRefId"].Single());
    }

    [Fact]
    public async Task ALeadWithNoCustomerAndAnUnknownReason_ReportsTheGeneratedCopyVerbatim()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/leads", new CreateLeadRequest
        {
            Id = Guid.NewGuid(),
            FullName = "   ",
            PhoneNumber = new string('c', 33),
            Gender = Gender.Female,
            ConsentGiven = true,
            ReferredOrTreated = false,
            ReasonNotPurchasedRefId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["FullName", "PhoneNumber", "ReasonNotPurchasedRefId"], errors);
        Assert.Equal("'Full Name' must not be empty.", errors["FullName"].Single());
        Assert.Equal(
            "The length of 'Phone Number' must be 32 characters or fewer. You entered 33 characters.",
            errors["PhoneNumber"].Single());
        Assert.Equal(
            "ReasonNotPurchasedRefId must reference an existing, active ReasonNotPurchased reference-data item.",
            errors["ReasonNotPurchasedRefId"].Single());
    }

    /// <summary>The Sale is the request worth checking whole: it carries the most rules, and it is
    /// the one ADR-0002 costed at 7 + 3n + n(n-1)/2 sequential reference-data reads before the
    /// snapshot replaced them with one.</summary>
    [Fact]
    public async Task ASaleFailingAcrossEveryTopic_ReportsEachAgainstItsOwnField()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/sales", new CreateSaleRequest
        {
            Id = Guid.NewGuid(),
            FullName = "Amina Okoro",
            Gender = Gender.Female,
            ConsentGiven = true,
            ReferredOrTreated = false,
            // Custom, but with no prescription typed out and no pupil distance — and the
            // DotGlasses order flag is only meaningful on Custom, so it must NOT be reported here.
            LensRangeType = LensRangeType.Custom,
            OrderFromDotGlasses = true,
            FrameCoverage = (ContractFrameCoverage)99,
            FrameColourRefId = Guid.NewGuid(),
            CoatingRefIds = [],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["FrameCoverage", "FrameColourRefId", "LensRangeType", "PupilDistanceMm", "CoatingRefIds"], errors);
        Assert.DoesNotContain("OrderFromDotGlasses", errors.Keys);
        Assert.Equal("'Frame Coverage' has a range of values which does not include '99'.", errors["FrameCoverage"].Single());
        Assert.Equal(
            "SphereLeft and SphereRight are required for a Custom LensRangeType.",
            errors["LensRangeType"].Single());
        Assert.Equal("Choose at least one coating.", errors["CoatingRefIds"].Single());
    }

    /// <summary>The per-eye lens power fields carry range-neutral names (lens-power ticket 01), and
    /// a rejection has to come back keyed on them — FormErrors and the Admin Portal's
    /// Form.{PropertyName} remap key off exactly these strings.</summary>
    [Fact]
    public async Task ACustomPrescriptionOutOfRange_IsReportedAgainstTheRangeNeutralFieldNames()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/tests", new CreateTestRequest
        {
            Id = Guid.NewGuid(),
            Gender = Gender.Female,
            Outcome = TestOutcome.NeedsGlasses,
            LensRangeType = LensRangeType.Custom,
            SphereLeft = 10.10m,
            SphereRight = 0m,
            CylinderLeft = -0.30m,
            AxisRight = 180.5m,
            AddLeft = 3.25m,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["SphereLeft", "CylinderLeft", "AxisRight", "AddLeft", "LensTypeRefId"], errors);
        Assert.Equal("SphereLeft must be between -10 and 10 in 0.25 increments.", errors["SphereLeft"].Single());
        Assert.Equal("AxisRight must be a whole number of degrees between 0 and 180.", errors["AxisRight"].Single());
    }

    /// <summary>
    /// A device can still hold records queued before the rename: the outbox posts each payload's
    /// stored JSON as-is, so it arrives spelled sphereLeft and so on. Unknown names are
    /// ignored by the binder, so without the legacy aliases the prescription would vanish and the
    /// record be refused for having no spheres. Asserted through a rejection, because a value that
    /// comes back keyed on SphereLeft is proof the old name was bound to the new property.
    /// </summary>
    [Fact]
    public async Task AQueuedPayloadWithThePreRenameFieldNames_StillBindsItsPrescription()
    {
        var client = CreateAuthenticatedClient();
        var payload = $$"""
            {
              "id": "{{Guid.NewGuid()}}",
              "gender": {{(int)Gender.Female}},
              "outcome": {{(int)TestOutcome.NeedsGlasses}},
              "lensRangeType": {{(int)LensRangeType.Custom}},
              "customSphereLeft": 10.10,
              "customSphereRight": 0,
              "customCylinderLeft": -0.30,
              "customAxisRight": 180.5,
              "customAddPowerLeft": 3.25
            }
            """;

        var response = await client.PostAsync("api/v1/tests", new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["SphereLeft", "CylinderLeft", "AxisRight", "AddLeft", "LensTypeRefId"], errors);
    }

    /// <summary>The ValidationProblemDetails body as key → messages.</summary>
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

    private HttpClient CreateAuthenticatedClient() => factory.CreateTechnicianClient(CallerOutlet);
}

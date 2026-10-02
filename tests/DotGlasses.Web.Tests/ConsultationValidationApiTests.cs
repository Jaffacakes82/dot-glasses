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
/// strings the rule module produces — plain instructions for anything a form control can cause,
/// FluentValidation's generated copy only for what no form can (an empty Id, an out-of-enum value).
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
        Assert.Equal("This record can't be saved as it was sent. Open it, check each answer and save it again.", errors["Id"].Single());
        Assert.Equal("This record can't be saved as it was sent. Open it, check each answer and save it again.", errors["Gender"].Single());
        Assert.Equal("Enter an age between 0 and 120.", errors["AgeYears"].Single());
        Assert.Equal(
            "Keep the other occupation to 200 characters or fewer.",
            errors["OccupationOtherText"].Single());
        Assert.Equal(
            "Choose an occupation from the list.",
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
            CustomerToldPrice = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["FullName", "PhoneNumber", "ReasonNotPurchasedRefId"], errors);
        Assert.Equal("Enter the customer's full name.", errors["FullName"].Single());
        Assert.Equal(
            "Keep the phone number to 32 characters or fewer.",
            errors["PhoneNumber"].Single());
        Assert.Equal(
            "Choose a reason not purchased.",
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
        Assert.Equal("This record can't be saved as it was sent. Open it, check each answer and save it again.", errors["FrameCoverage"].Single());
        Assert.Equal(
            "Choose a sphere for each eye.",
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
            CylinderRight = -1.00m,
            AxisRight = 180.5m,
            AddLeft = 3.25m,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["SphereLeft", "CylinderLeft", "AxisRight", "AddLeft", "LensTypeRefId"], errors);
        Assert.Equal("Sphere (left): choose a value between -10 and 10, in steps of 0.25.", errors["SphereLeft"].Single());
        Assert.Equal("Axis (right): choose a whole number of degrees from 0 to 180.", errors["AxisRight"].Single());
    }

    /// <summary>Allowed values (lens-power ticket 02): the shop sells no positive cylinder. A
    /// device that queued one before the release lands it on Failed records against the cylinder
    /// control, which this key is what makes possible.</summary>
    [Fact]
    public async Task APositiveCylinder_IsRefusedAgainstThatEyesCylinder()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/tests", CustomTest(test =>
        {
            test.CylinderLeft = 0.50m;
            test.AxisLeft = 90m;
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["CylinderLeft"], errors);
        Assert.Equal("Cylinder (left): choose a value between -6 and 0, in steps of 0.25.", errors["CylinderLeft"].Single());
    }

    /// <summary>Axis (lens-power ticket 02): required with a cylinder, refused without one.</summary>
    [Fact]
    public async Task AnAxisThatDisagreesWithItsCylinder_IsRefusedAgainstThatEyesAxis()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/tests", CustomTest(test =>
        {
            test.CylinderLeft = -1.25m;
            test.AxisRight = 45m;
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["AxisLeft", "AxisRight"], errors);
        Assert.Equal("Axis (left): choose an axis from 0 to 180 — Cylinder (left) isn't 0.00.", errors["AxisLeft"].Single());
        Assert.Equal("Axis (right): clear the axis — it only applies when Cylinder (right) isn't 0.00.", errors["AxisRight"].Single());
    }

    /// <summary>Lens type (lens-power ticket 02): an add of 0.00 is no add, so it takes no lens
    /// type — the shop's behaviour, and the reverse of what the Field App used to ask.</summary>
    [Fact]
    public async Task ALensTypeWithAnAddOfZero_IsRefusedAgainstTheLensType()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/tests", CustomTest(test =>
        {
            test.AddLeft = 0.00m;
            test.AddRight = 0.00m;
            test.LensTypeRefId = Guid.NewGuid();
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorsAsync(response);

        AssertKeys(["LensTypeRefId"], errors);
        Assert.Equal("A lens type only applies to a lens with an add power — remove the lens type.", errors["LensTypeRefId"].Single());
    }

    [Fact]
    public async Task AShopStylePrescription_WithAnAddOfZeroAndNoLensType_IsStored()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/tests", new CreateTestRequest
        {
            Id = Guid.NewGuid(),
            Gender = Gender.Female,
            Outcome = TestOutcome.NeedsGlasses,
            LensRangeType = LensRangeType.Custom,
            SphereLeft = -2.25m,
            SphereRight = 1.50m,
            CylinderLeft = -6.00m,
            AxisLeft = 0m,
            AddLeft = 0.00m,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stored = await response.Content.ReadFromJsonAsync<TestDto>();
        Assert.Equal(-6.00m, stored!.CylinderLeft);
        Assert.Equal(0m, stored.AxisLeft);
        Assert.Null(stored.LensTypeRefId);
    }

    /// <summary>A Custom-prescription Test nothing objects to, adjusted by each case to break only
    /// the rule it is about. A Test rather than a Sale because it carries the same lens fields
    /// with no frame colour or coating set whose reference data would muddy the exact key
    /// assertions.</summary>
    private static CreateTestRequest CustomTest(Action<CreateTestRequest> adjust)
    {
        var test = new CreateTestRequest
        {
            Id = Guid.NewGuid(),
            Gender = Gender.Female,
            Outcome = TestOutcome.NeedsGlasses,
            LensRangeType = LensRangeType.Custom,
            SphereLeft = -1.00m,
            SphereRight = -1.25m,
        };
        adjust(test);
        return test;
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

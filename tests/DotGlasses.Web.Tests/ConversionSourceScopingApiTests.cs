using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DotGlasses.Application.Common;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Web.Auth;
using Microsoft.Extensions.DependencyInjection;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;

namespace DotGlasses.Web.Tests;

/// <summary>
/// What a caller actually gets when a conversion names a source record it cannot resolve —
/// recorded here because it is easy to assume otherwise.
///
/// The API boundary refuses this *before* the service ever runs: CreateLeadRequestValidator and
/// CreateSaleRequestValidator both look the source up through the same hierarchy-scoped
/// repository the service uses, so an out-of-scope source is indistinguishable from a
/// non-existent one and both come back as a field-level validation failure keyed on
/// SourceTestId/SourceLeadId. LeadService/SaleService's own refusal (ticket 16) therefore sits
/// behind this net rather than in front of it: it is the guarantee that the *service* cannot
/// half-complete, not a message a technician will see today.
///
/// That ordering is worth pinning. If the validator's source check were ever dropped as
/// redundant, the request would fall through to the service, and these assertions would change
/// from a SourceTestId-keyed failure to an empty-string-keyed one — still a 400, still terminal
/// for the Field App's outbox, but a different response shape.
/// </summary>
[Collection(WebApiCollection.Name)]
public class ConversionSourceScopingApiTests(CustomWebApplicationFactory factory)
{
    /// <summary>A retail point deep in the seeded tree. Nothing this caller writes is visible
    /// from anywhere else, and nothing above or beside it is visible to them.</summary>
    private const string CallerOutlet = "/1/2/3/4/";

    [Fact]
    public async Task RecordingALeadAgainstAnUnresolvableSourceTest_IsRefusedByTheValidatorOnSourceTestId()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/leads", new CreateLeadRequest
        {
            Id = Guid.NewGuid(),
            SourceTestId = Guid.NewGuid(),
            FullName = "Amina Okoro",
            PhoneNumber = "0700111222",
            AgeYears = 42,
            Gender = Gender.Female,
            ConsentGiven = true,
            ReferredOrTreated = false,
            ReasonNotPurchasedRefId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorKeysAsync(response);
        Assert.Contains(nameof(CreateLeadRequest.SourceTestId), errors);
        Assert.DoesNotContain(string.Empty, errors);
    }

    [Fact]
    public async Task RecordingASaleAgainstAnUnresolvableSourceLead_IsRefusedByTheValidatorOnSourceLeadId()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/sales", new CreateSaleRequest
        {
            Id = Guid.NewGuid(),
            SourceLeadId = Guid.NewGuid(),
            FullName = "Amina Okoro",
            PhoneNumber = "0700111222",
            AgeYears = 42,
            Gender = Gender.Female,
            ConsentGiven = true,
            ReferredOrTreated = false,
            LensRangeType = LensRangeType.Custom,
            OrderFromDotGlasses = false,
            FrameCoverage = ContractFrameCoverage.FullFrame,
            CoatingRefIds = [],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errors = await ErrorKeysAsync(response);
        Assert.Contains(nameof(CreateSaleRequest.SourceLeadId), errors);
        Assert.DoesNotContain(string.Empty, errors);
    }

    /// <summary>The keys of a ValidationProblemDetails body — the empty string is the form-level
    /// slot DomainRuleViolationFilter uses, so its absence is what says the service was never
    /// reached.</summary>
    private static async Task<IReadOnlyList<string>> ErrorKeysAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().Select(e => e.Name).ToList();
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

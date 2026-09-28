using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Contracts.Auth;

namespace DotGlasses.Web.Tests.DomainRejections;

/// <summary>
/// The JSON-API half of DomainRuleViolationFilter (ADR-0003): a rejection thrown by
/// AuthController.SwitchOrg arrives as a 400 ValidationProblemDetails carrying its own
/// copy, without the controller catching anything.
/// </summary>
public class DomainRuleViolationApiTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private HttpClient CreateAuthenticatedClient() => factory.CreateTechnicianClient("/1/");

    [Fact]
    public async Task SwitchOrg_ToAnOrgTheUserIsNotAssignedTo_ReturnsTheRejectionAsAValidationProblem()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("api/v1/auth/switch-org", new SwitchOrgRequest { OrgNodeId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // Keyed on the empty string — the form-level slot ValidationProblem() uses, which
        // DotGlasses.App's SyncService/FormErrors already understands.
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var messages = body.RootElement.GetProperty("errors").GetProperty(string.Empty);

        Assert.Equal(
            "That isn't an active retail point you're assigned to.",
            messages[0].GetString());
    }
}

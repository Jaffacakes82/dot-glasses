using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using DotGlasses.Application.Common;
using DotGlasses.Contracts.WidgetExamples;
using DotGlasses.Web.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests;

public class WidgetExamplesApiTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static HttpClient CreateAuthenticatedClient(CustomWebApplicationFactory factory, string userName, params string[] roles)
    {
        var client = factory.CreateClient();
        var tokenService = factory.Services.GetRequiredService<IJwtTokenService>();

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, userName),
            new(DotGlassesClaimTypes.HierarchyPath, "/1/"),
            ..roles.Select(role => new Claim(ClaimTypes.Role, role)),
        ];

        var (token, _) = tokenService.CreateToken(claims);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("api/v1/widget-examples");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithValidToken_ReturnsOk()
    {
        var client = CreateAuthenticatedClient(factory, "reader", RoleNames.User);

        var response = await client.GetAsync("api/v1/widget-examples");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_WithUserRoleOnly_ReturnsForbidden()
    {
        // Exercises the RBAC policy example (brief 3.2b): "WidgetExample.Create" requires
        // Admin or Manager — a plain User is authenticated but not authorized for this action.
        var client = CreateAuthenticatedClient(factory, "plain-user", RoleNames.User);
        var request = new CreateWidgetExampleRequest { Id = Guid.NewGuid(), Name = "Test", HierarchyPath = "/1/" };

        var response = await client.PostAsJsonAsync("api/v1/widget-examples", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAdminRole_ReturnsCreated_AndIsThenRetrievable()
    {
        var client = CreateAuthenticatedClient(factory, "admin", RoleNames.Admin);
        var request = new CreateWidgetExampleRequest { Id = Guid.NewGuid(), Name = "Test Widget", HierarchyPath = "/1/" };

        var createResponse = await client.PostAsJsonAsync("api/v1/widget-examples", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var getResponse = await client.GetAsync($"api/v1/widget-examples/{request.Id}");
        getResponse.EnsureSuccessStatusCode();

        var dto = await getResponse.Content.ReadFromJsonAsync<WidgetExampleDto>();
        Assert.NotNull(dto);
        Assert.Equal("Test Widget", dto!.Name);
    }
}

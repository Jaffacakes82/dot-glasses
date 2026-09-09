using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DotGlasses.Contracts.Auth;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Web.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests;

/// <summary>
/// POST /api/v1/auth/change-password — the JWT-authenticated counterpart to the Admin Portal's
/// cookie-based AccountController.ChangePassword (ticket 04 of
/// .scratch/field-app-nonprod-feedback-2026-09-09), added so the Field App's Settings screen can
/// offer a real Change Password action instead of nothing at all.
/// </summary>
[Collection(WebApiCollection.Name)]
public class ChangePasswordApiTests(CustomWebApplicationFactory factory)
{
    private const string Password = "TestPassw0rd!";

    [Fact]
    public async Task ACorrectCurrentPassword_ChangesItAndTheNewOneSignsIn()
    {
        var (client, userName) = await CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("api/v1/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = Password,
            NewPassword = "NewTestPassw0rd!",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginResponse = await factory.CreateClient().PostAsJsonAsync("api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = "NewTestPassw0rd!",
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task AWrongCurrentPassword_IsRejectedAgainstCurrentPasswordField()
    {
        var (client, _) = await CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("api/v1/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword1!",
            NewPassword = "NewTestPassw0rd!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Contains("CurrentPassword", errors.Keys);
    }

    [Fact]
    public async Task ANewPasswordThatFailsThePolicy_IsRejectedAgainstNewPasswordField()
    {
        var (client, _) = await CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("api/v1/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = Password,
            NewPassword = "short",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Contains("NewPassword", errors.Keys);
    }

    [Fact]
    public async Task AnEmptyRequest_IsRejectedByTheValidator()
    {
        var (client, _) = await CreateSignedInClientAsync();

        var response = await client.PostAsJsonAsync("api/v1/auth/change-password", new ChangePasswordRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Contains("CurrentPassword", errors.Keys);
        Assert.Contains("NewPassword", errors.Keys);
    }

    [Fact]
    public async Task AnUnauthenticatedCaller_IsRejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/v1/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = Password,
            NewPassword = "NewTestPassw0rd!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(HttpClient Client, string UserName)> CreateSignedInClientAsync()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var userName = $"change-password-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = userName,
            EmailConfirmed = true,
            HierarchyPath = "/1/",
        };
        var created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

        var client = factory.CreateClient();
        var tokenService = factory.Services.GetRequiredService<IJwtTokenService>();
        var (token, _) = tokenService.CreateToken([new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, userName);
    }

    /// <summary>The ValidationProblemDetails body as key → messages.</summary>
    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ErrorsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().ToDictionary(
            property => property.Name,
            IReadOnlyList<string> (property) => property.Value.EnumerateArray().Select(v => v.GetString()!).ToList());
    }
}

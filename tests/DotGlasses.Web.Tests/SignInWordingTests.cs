using System.Net;
using System.Net.Http.Json;
using DotGlasses.Contracts.Auth;
using DotGlasses.Web.Tests.DomainRejections;

namespace DotGlasses.Web.Tests;

/// <summary>The sign-in name is an email address, and the Admin Portal's sign-in page says so —
/// in its label and when a sign-in fails (Spec D, "Wording").</summary>
[Collection(WebApiCollection.Name)]
public class SignInWordingTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task AFailedAdminPortalSignIn_SaysEmailOrPasswordIsIncorrect()
    {
        var client = factory.CreateClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Account/Login");

        var response = await client.PostAsync("/Account/Login", AdminPortalFactory.Form(token,
            ("UserName", "nobody@dotglasses.test"),
            ("Password", "not-the-password")));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Email or password is incorrect.", html);
        Assert.Contains(">Email</label>", html);
        Assert.DoesNotContain("username or password", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ABlankAdminPortalSignIn_AsksForTheEmailAndThePassword()
    {
        var client = factory.CreateClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Account/Login");

        var html = await (await client.PostAsync("/Account/Login", AdminPortalFactory.Form(token))).Content.ReadAsStringAsync();

        Assert.Contains("Enter your email address.", html);
        Assert.Contains("Enter your password.", html);
    }

    [Fact]
    public async Task AFailedFieldAppSignIn_IsUnauthorized()
    {
        // The Field App writes its own "Email or password is incorrect." for any non-success.
        var response = await factory.CreateClient().PostAsJsonAsync("api/v1/auth/login",
            new LoginRequest { UserName = "nobody@dotglasses.test", Password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

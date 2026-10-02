using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using DotGlasses.Application.Common;
using DotGlasses.Application.Notifications;
using DotGlasses.Contracts.Auth;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests;

/// <summary>The real host with the email sender swapped for one that records what it was asked
/// to send — the reset link is only ever emailed, so the recording is the only place a test can
/// read it.</summary>
public class ForgotPasswordFactory : CustomWebApplicationFactory
{
    public RecordingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddScoped<IEmailSender>(_ => Emails);
        });
    }
}

public sealed class RecordingEmailSender : IEmailSender
{
    public sealed record Sent(string Kind, string ToEmail, string RecipientName, string Url);

    private readonly ConcurrentQueue<Sent> _sent = new();

    public IReadOnlyList<Sent> ResetsTo(string email) =>
        _sent.Where(s => s.Kind == "reset" && string.Equals(s.ToEmail, email, StringComparison.OrdinalIgnoreCase)).ToList();

    public Task SendPasswordSetupInviteAsync(string toEmail, string recipientName, string setPasswordUrl, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(new Sent("invite", toEmail, recipientName, setPasswordUrl));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string recipientName, string resetUrl, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(new Sent("reset", toEmail, recipientName, resetUrl));
        return Task.CompletedTask;
    }
}

/// <summary>
/// "Forgot password?" on both sign-in pages (Spec C). Anyone can ask for any address, so the
/// answer never says whether it has an account; an email goes to an Active or Invited account
/// only, at most once every five minutes; the link works once; and the person ends up back at
/// the app they asked from.
/// </summary>
public class ForgotPasswordTests(ForgotPasswordFactory factory) : IClassFixture<ForgotPasswordFactory>
{
    private const string Acknowledgement = "we&#x27;ve sent a link";
    private const string Password = "TestPassw0rd!";
    private const string NewPassword = "An0ther!Passw0rd";

    // --- The answer is the same for every address --------------------------------------------------

    [Fact]
    public async Task ThePortalPage_AnswersTheSameWay_ForAKnownAnUnknownAndASuspendedAddress()
    {
        var known = await CreateUserAsync();
        var suspended = await CreateUserAsync(suspended: true);

        var answers = new List<string>();
        foreach (var email in new[] { known, suspended, $"nobody-{Guid.NewGuid():N}@test.local" })
        {
            var html = await RequestFromPortalAsync(email);
            Assert.Contains(Acknowledgement, html);
            Assert.DoesNotContain("SetPassword", html);
            answers.Add(WithoutAntiforgeryTokens(html));
        }

        Assert.Single(answers.Distinct());
    }

    [Fact]
    public async Task TheApi_AnswersTheSameWay_ForAKnownAnUnknownAndASuspendedAddress_AndNeverReturnsTheLink()
    {
        var known = await CreateUserAsync();
        var suspended = await CreateUserAsync(suspended: true);

        var bodies = new List<string>();
        foreach (var email in new[] { known, suspended, $"nobody-{Guid.NewGuid():N}@test.local" })
        {
            var response = await factory.CreateClient().PostAsJsonAsync("api/v1/auth/forgot-password", new ForgotPasswordRequest { Email = email });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        using var body = System.Text.Json.JsonDocument.Parse(Assert.Single(bodies.Distinct()));
        var property = Assert.Single(body.RootElement.EnumerateObject());
        Assert.Equal(("message", "If that email has an account, we've sent a link."), (property.Name, property.Value.GetString()));
    }

    // --- Who is emailed ---------------------------------------------------------------------------

    [Fact]
    public async Task AnActiveAccount_GetsOneEmail_AndNoSecondOneWithinFiveMinutes()
    {
        var email = await CreateUserAsync();

        await RequestFromPortalAsync(email);
        await RequestFromPortalAsync(email);
        await factory.CreateClient().PostAsJsonAsync("api/v1/auth/forgot-password", new ForgotPasswordRequest { Email = email });

        var sent = Assert.Single(factory.Emails.ResetsTo(email));
        Assert.Contains("/Account/SetPassword", sent.Url);

        // Once the five minutes have passed, the next request is emailed again.
        await SetLastEmailedAsync(email, DateTimeOffset.UtcNow.AddMinutes(-6));
        await RequestFromPortalAsync(email);
        Assert.Equal(2, factory.Emails.ResetsTo(email).Count);
    }

    [Fact]
    public async Task ASuspendedAccount_AndAnUnknownAddress_GetNoEmail()
    {
        var suspended = await CreateUserAsync(suspended: true);
        var unknown = $"nobody-{Guid.NewGuid():N}@test.local";

        await RequestFromPortalAsync(suspended);
        await RequestFromPortalAsync(unknown);

        Assert.Empty(factory.Emails.ResetsTo(suspended));
        Assert.Empty(factory.Emails.ResetsTo(unknown));
    }

    [Fact]
    public async Task AnInvitedAccountThatNeverSetAPassword_GetsTheEmail_AndTheLinkLetsThemSetOne()
    {
        var email = await CreateUserAsync(withPassword: false);

        await RequestFromPortalAsync(email);
        var link = Assert.Single(factory.Emails.ResetsTo(email)).Url;

        var response = await SetPasswordAsync(link, NewPassword);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(HttpStatusCode.Found, (await SignInAsync(email, NewPassword)).StatusCode);
    }

    // --- The link ----------------------------------------------------------------------------------

    [Fact]
    public async Task ALinkAskedForFromTheAdminPortal_ReturnsToItsSignIn_AndWorksOnlyOnce()
    {
        var email = await CreateUserAsync();
        await RequestFromPortalAsync(email);
        var link = Assert.Single(factory.Emails.ResetsTo(email)).Url;

        var first = await SetPasswordAsync(link, NewPassword);
        Assert.Equal(HttpStatusCode.Found, first.StatusCode);
        Assert.Equal("/Account/Login", PathOf(first));

        // The password changed, so the link is spent.
        var second = await SetPasswordAsync(link, "Y3tAnother!Passw0rd");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("Invalid token", await second.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Found, (await SignInAsync(email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email, "Y3tAnother!Passw0rd")).StatusCode);
    }

    [Fact]
    public async Task ALinkAskedForFromTheFieldApp_ReturnsToTheFieldAppsConfiguredSignIn_WhateverTheLinkSays()
    {
        var email = await CreateUserAsync();
        await factory.CreateClient().PostAsJsonAsync("api/v1/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        var link = Assert.Single(factory.Emails.ResetsTo(email)).Url;
        Assert.Contains("app=field", link);

        // A crafted link can say which app, never where it lives.
        var response = await SetPasswordAsync(link + "&returnUrl=https%3A%2F%2Fevil.example%2F", NewPassword);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("http://localhost:5253/login?passwordSet=1", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task TheSignInPage_OffersForgotPassword()
    {
        var html = await factory.CreateClient().GetStringAsync("/Account/Login");

        Assert.Contains("href=\"/Account/ForgotPassword\"", html);
        Assert.Contains("Forgot password?", html);
    }

    // --- Helpers -----------------------------------------------------------------------------------

    private async Task<string> RequestFromPortalAsync(string email)
    {
        var client = factory.CreateClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Account/ForgotPassword");
        var response = await client.PostAsync("/Account/ForgotPassword", AdminPortalFactory.Form(token, ("Email", email)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Opens the emailed link and submits the set-password form it renders.</summary>
    private async Task<HttpResponseMessage> SetPasswordAsync(string link, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var uri = new Uri(link);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, uri.PathAndQuery);

        return await client.PostAsync("/Account/SetPassword", AdminPortalFactory.Form(token,
            ("UserId", query["userId"].ToString()),
            ("Token", query["token"].ToString()),
            ("App", query.TryGetValue("app", out var app) ? app.ToString() : string.Empty),
            ("Password", password)));
    }

    private async Task<HttpResponseMessage> SignInAsync(string email, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", AdminPortalFactory.Form(token, ("UserName", email), ("Password", password)));
    }

    private async Task<string> CreateUserAsync(bool withPassword = true, bool suspended = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(RoleNames.User))
        {
            await roles.CreateAsync(new IdentityRole<Guid>(RoleNames.User));
        }

        var email = $"forgot-{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = email, Email = email, FullName = "Forgetful Person" };
        var created = withPassword ? await users.CreateAsync(user, Password) : await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));
        Assert.True((await users.AddToRoleAsync(user, RoleNames.User)).Succeeded);

        if (suspended)
        {
            await users.SetLockoutEnabledAsync(user, true);
            await users.SetLockoutEndDateAsync(user, UserSuspension.LockoutEnd);
        }

        db.UserOrgAssignments.Add(new UserOrgAssignment
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrgNodeId = OrganisationSeedConfiguration.KenyaRetailPointId, CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return email;
    }

    private async Task SetLastEmailedAsync(string email, DateTimeOffset when)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>();
        await db.Users.Where(u => u.Email == email).ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordResetEmailSentAtUtc, when));
    }

    private static string PathOf(HttpResponseMessage response)
    {
        var location = response.Headers.Location!;
        return (location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location)).AbsolutePath;
    }

    private static string WithoutAntiforgeryTokens(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "name=\"__RequestVerificationToken\"[^>]*>", string.Empty);
}

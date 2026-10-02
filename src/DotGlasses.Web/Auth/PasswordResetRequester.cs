using DotGlasses.Application.Notifications;
using DotGlasses.Application.Users;
using DotGlasses.Web.Controllers;

namespace DotGlasses.Web.Auth;

/// <summary>Which sign-in page a password-reset request came from, and so which one the person
/// is returned to once they have set a new password.</summary>
public static class RequestingApp
{
    public const string AdminPortal = "admin";
    public const string FieldApp = "field";
}

/// <summary>Where the Field App lives, for sending someone back to its sign-in page after a
/// password reset. A fixed, configured address — never taken from the reset link, which anyone
/// can craft: an open redirect on a password page is a phishing aid. One build serves both
/// environments, so the address is looked up by the Admin Portal host the request arrived on,
/// falling back to <see cref="BaseUrl"/> (local development).</summary>
public class FieldAppOptions
{
    public const string SectionName = "FieldApp";

    public string BaseUrl { get; set; } = "http://localhost:5253/";

    public Dictionary<string, string> BaseUrlByAdminHost { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string SignInUrl(string? adminHost)
    {
        var baseUrl = adminHost is not null && BaseUrlByAdminHost.TryGetValue(adminHost, out var mapped) ? mapped : BaseUrl;
        return $"{baseUrl.TrimEnd('/')}/login?passwordSet=1";
    }
}

/// <summary>
/// The one path a "Forgot password?" request takes, from either sign-in page: ask
/// <see cref="IPasswordResetService"/> whether an email is due, and if so send it with a link to
/// the existing set-password page. The link is only ever emailed — never shown on screen or
/// returned by the API — and the caller answers with the same message whatever happened here.
/// </summary>
public class PasswordResetRequester(IPasswordResetService passwordResetService, IEmailSender emailSender, LinkGenerator linkGenerator)
{
    public async Task RequestAsync(HttpContext httpContext, string? email, string requestingApp, CancellationToken cancellationToken)
    {
        if (await passwordResetService.RequestAsync(email, cancellationToken) is not { } link)
        {
            return;
        }

        var url = linkGenerator.GetUriByAction(
            httpContext,
            action: nameof(AccountController.SetPassword),
            controller: "Account",
            values: new { userId = link.UserId, token = link.Token, app = requestingApp })!;

        await emailSender.SendPasswordResetAsync(link.Email, link.RecipientName, url, cancellationToken);
    }
}

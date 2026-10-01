using Microsoft.AspNetCore.Identity;

namespace DotGlasses.Infrastructure.Identity;

/// <summary>An account. It carries no org of its own: where a user has access is entirely their
/// UserOrgAssignment rows, re-read on every request (ADR-0006), and the Field App's current
/// location lives in its token, never here.</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Stamped on every successful sign-in, both the MVC cookie path (AccountController)
    /// and the API JWT path (AuthController) — a RetailPoint User almost never touches the Admin
    /// Portal, so only stamping the cookie path would leave this permanently null for most users.</summary>
    public DateTimeOffset? LastLoginUtc { get; set; }

    /// <summary>Nullable — the three DevUserSeeder dev accounts predate this field and have
    /// none; User Directory falls back to UserName/Email for display when absent.</summary>
    public string? FullName { get; set; }

    /// <summary>When the last "Forgot password?" email was sent to this account — what limits
    /// them to one every few minutes (IPasswordResetService). Stored here rather than in memory
    /// so the limit holds across replicas.</summary>
    public DateTimeOffset? PasswordResetEmailSentAtUtc { get; set; }

    /// <summary>The one fallback rule for "what do we call this user" — every caller-facing
    /// display of a user's name needs it, parameterized only by what "unset" should render as in
    /// that context (an admin table cell wants "—"; a technician's own device greeting wants
    /// nothing at all rather than a literal em dash).</summary>
    public string DisplayName(string fallback = "—") =>
        string.IsNullOrWhiteSpace(FullName) ? UserName ?? fallback : FullName;
}

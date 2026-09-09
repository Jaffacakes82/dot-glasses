namespace DotGlasses.Contracts.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>Falls back to the username/email server-side when the user has no FullName set —
    /// same fallback UserAdminService already uses for User Directory display.</summary>
    public string DisplayName { get; set; } = string.Empty;
}

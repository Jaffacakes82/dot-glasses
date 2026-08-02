namespace DotGlasses.App.Auth;

/// <summary>
/// [OPEN] In-memory only — lost on page refresh. A real field deployment needs the token
/// persisted (e.g. alongside the IndexedDB outbox) so a field agent isn't forced to re-login
/// after every browser restart; deferred as a simplification for this scaffold.
/// </summary>
public class AuthTokenStore
{
    public string? AccessToken { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public bool IsAuthenticated => AccessToken is not null && ExpiresAtUtc is { } expires && expires > DateTimeOffset.UtcNow;

    public void SetToken(string accessToken, DateTimeOffset expiresAtUtc)
    {
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Clear()
    {
        AccessToken = null;
        ExpiresAtUtc = null;
    }
}

using System.Text.Json;
using Microsoft.JSInterop;

namespace DotGlasses.App.Auth;

/// <summary>
/// Holds the API access token, persisted to IndexedDB so it survives a page refresh or browser
/// restart. Previously in-memory only, which meant a field technician was silently signed out by
/// any refresh and had to re-authenticate — an operation that needs connectivity, directly
/// contradicting the login screen's "log in once online, then work fully offline" promise.
///
/// Persisting a bearer token on what may be a shared device is a real trade-off, taken
/// deliberately: it is the only way to deliver genuine offline working. It is why
/// <see cref="ClearAsync"/> and the Field App's sign-out action ship alongside this, rather than
/// being left for later — a persisted token with no way to end the session is a handover risk.
///
/// The in-memory properties stay synchronous so AuthorizationMessageHandler's SendAsync can read
/// them without blocking; IndexedDB is only touched on initialize, sign-in and sign-out.
/// </summary>
public class AuthTokenStore(IJSRuntime jsRuntime)
{
    private const string StorageKey = "auth-token";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string? AccessToken { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public bool IsAuthenticated => AccessToken is not null && ExpiresAtUtc is { } expires && expires > DateTimeOffset.UtcNow;

    /// <summary>
    /// Rehydrates a previously persisted token. Called once at start-up, before the host runs, so
    /// the very first render already knows whether the user is signed in — otherwise Home would
    /// bounce to the login page for a moment on every launch. An expired token is discarded here
    /// rather than left to fail its first API call.
    /// </summary>
    public async Task InitializeAsync()
    {
        var json = await jsRuntime.InvokeAsync<string?>("dotGlassesIdb.kvGet", StorageKey);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        PersistedToken? persisted;
        try
        {
            persisted = JsonSerializer.Deserialize<PersistedToken>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Corrupt or superseded shape — treat as signed out rather than failing start-up.
            await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvRemove", StorageKey);
            return;
        }

        if (persisted is null || persisted.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvRemove", StorageKey);
            return;
        }

        AccessToken = persisted.AccessToken;
        ExpiresAtUtc = persisted.ExpiresAtUtc;
    }

    public async Task SetTokenAsync(string accessToken, DateTimeOffset expiresAtUtc)
    {
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;

        var json = JsonSerializer.Serialize(new PersistedToken(accessToken, expiresAtUtc), JsonOptions);
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvSet", StorageKey, json);
    }

    public async Task ClearAsync()
    {
        AccessToken = null;
        ExpiresAtUtc = null;
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvRemove", StorageKey);
    }

    private sealed record PersistedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
}

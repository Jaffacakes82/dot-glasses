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

    /// <summary>Separate key, deliberately never cleared by <see cref="ClearAsync"/> — the *device*
    /// remembers its last current location across a sign-out and the next sign-in (stories 23/30),
    /// which is a different lifetime from the session token itself. Read by Login.razor to populate
    /// LoginRequest.PreferredLocationId.</summary>
    private const string LastLocationStorageKey = "last-location-id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string? AccessToken { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public string? DisplayName { get; private set; }

    /// <summary>The current location this session's token carries, or null when it carries none
    /// (several eligible locations and none picked yet, or none eligible at all) — set from
    /// LoginResponse/SwitchOrg's response by <see cref="SetTokenAsync"/>, never derived locally.</summary>
    public Guid? CurrentLocationId { get; private set; }

    public string? CurrentLocationName { get; private set; }

    /// <summary>The last location this device successfully recorded at, independent of whether
    /// anyone is currently signed in — survives sign-out (see <see cref="LastLocationStorageKey"/>).
    /// Null on a brand-new device, which is exactly when the server has nothing to prefer either.</summary>
    public Guid? LastKnownLocationId { get; private set; }

    public bool IsAuthenticated => AccessToken is not null && ExpiresAtUtc is { } expires && expires > DateTimeOffset.UtcNow;

    /// <summary>Fires whenever the token (and so possibly CurrentLocationName) changes — sign-in,
    /// switch-org, or sign-out. MainLayout's persistent location line is the reason this exists: it
    /// renders outside any page's own lifecycle, so it needs a push rather than relying on the next
    /// unrelated render to happen to pick up a stale value after a switch.</summary>
    public event Action? Changed;

    /// <summary>
    /// Rehydrates a previously persisted token. Called once at start-up, before the host runs, so
    /// the very first render already knows whether the user is signed in — otherwise Home would
    /// bounce to the login page for a moment on every launch. An expired token is discarded here
    /// rather than left to fail its first API call. The remembered location is read regardless of
    /// whether the token itself is still valid, since it must survive a sign-out.
    /// </summary>
    public async Task InitializeAsync()
    {
        var lastLocationJson = await jsRuntime.InvokeAsync<string?>("dotGlassesIdb.kvGet", LastLocationStorageKey);
        if (!string.IsNullOrEmpty(lastLocationJson))
        {
            try
            {
                LastKnownLocationId = JsonSerializer.Deserialize<Guid?>(lastLocationJson, JsonOptions);
            }
            catch (JsonException)
            {
                await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvRemove", LastLocationStorageKey);
            }
        }

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
        DisplayName = persisted.DisplayName;
        CurrentLocationId = persisted.CurrentLocationId;
        CurrentLocationName = persisted.CurrentLocationName;
    }

    /// <summary>
    /// Called after login and after switch-org — both reissue the token, and both are how the
    /// client learns the token's current location. Whenever that location is non-null, it is also
    /// written to the device's own remembered-location key (outliving this token), so the *next*
    /// sign-in on this device — even under a different account — offers it as the preferred
    /// location. A null location (no location picked/eligible) leaves the last-known one untouched:
    /// it is still the best guess for next time, and a technician landing on the outlet picker
    /// hasn't un-recorded anywhere.
    /// </summary>
    public async Task SetTokenAsync(
        string accessToken,
        DateTimeOffset expiresAtUtc,
        string? displayName = null,
        Guid? currentLocationId = null,
        string? currentLocationName = null)
    {
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;
        DisplayName = displayName;
        CurrentLocationId = currentLocationId;
        CurrentLocationName = currentLocationName;

        var json = JsonSerializer.Serialize(
            new PersistedToken(accessToken, expiresAtUtc, displayName, currentLocationId, currentLocationName), JsonOptions);
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvSet", StorageKey, json);

        if (currentLocationId is { } id)
        {
            LastKnownLocationId = id;
            await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvSet", LastLocationStorageKey, JsonSerializer.Serialize(id, JsonOptions));
        }

        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        AccessToken = null;
        ExpiresAtUtc = null;
        DisplayName = null;
        CurrentLocationId = null;
        CurrentLocationName = null;
        // LastKnownLocationId is deliberately left alone — see LastLocationStorageKey's doc comment.
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvRemove", StorageKey);
        Changed?.Invoke();
    }

    private sealed record PersistedToken(
        string AccessToken, DateTimeOffset ExpiresAtUtc, string? DisplayName, Guid? CurrentLocationId, string? CurrentLocationName);
}

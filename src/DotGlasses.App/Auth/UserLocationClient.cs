using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Contracts.Auth;
using Microsoft.JSInterop;

namespace DotGlasses.App.Auth;

/// <summary>
/// The technician's own assignable selling points (UserOrgAssignment) and the ability to switch
/// the active one — backs Settings.razor's location list and OutletSelect.razor's post-login
/// picker. Switching re-issues the JWT server-side (new HierarchyPath/OrgNodeId/OrgLevel claims),
/// so SwitchOrgAsync always writes the fresh token into AuthTokenStore itself on success — a
/// caller that forgot to do this would keep stamping new records under the *old* location.
///
/// GetMyOrgsAsync is fetch-and-cache, the same write-through/fallback shape ReferenceDataClient
/// already uses for reference data and AuthTokenStore for the JWT itself: a successful load is
/// written through to IndexedDB, and a failed one (offline, unreachable, expired token) falls back
/// to whatever was cached last, rather than silently reporting "no locations assigned" — which
/// previously made Home/Settings look like the technician's org assignment had been revoked
/// whenever they simply had no connectivity.
/// </summary>
public interface IUserLocationClient
{
    Task<IReadOnlyList<AssignedOrgDto>> GetMyOrgsAsync();

    /// <summary>Null when the switch worked. Otherwise the technician stayed on the previous
    /// location and this is the message to show: one for having no connection (find one), another
    /// for the server refusing or failing (try again).</summary>
    Task<string?> SwitchOrgAsync(Guid orgNodeId);

    /// <summary>Non-null only when the most recent GetMyOrgsAsync call's live request failed *and*
    /// this device has no cached copy to fall back to — same shape as ReferenceDataClient's
    /// LoadError, so a caller can tell "genuinely no orgs assigned" (a real empty response, this
    /// stays null) apart from "couldn't check, and there's nothing to show yet."</summary>
    string? LoadError { get; }
}

public class UserLocationClient(HttpClient httpClient, AuthTokenStore tokenStore, IJSRuntime jsRuntime) : IUserLocationClient
{
    private const string StorageKey = "my-orgs-cache";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string? LoadError { get; private set; }

    public async Task<IReadOnlyList<AssignedOrgDto>> GetMyOrgsAsync()
    {
        try
        {
            var orgs = await httpClient.GetFromJsonAsync<List<AssignedOrgDto>>("api/v1/auth/my-orgs") ?? [];
            LoadError = null;
            await WriteCacheAsync(orgs);
            return orgs;
        }
        catch (Exception)
        {
            // Unreachable, offline, or the token has expired — fall back to the last-cached copy
            // rather than reporting an empty, unassigned-looking list.
            var cached = await TryLoadFromCacheAsync();
            LoadError = cached is null
                ? "Couldn't reach the server to load your locations, and this device has no saved copy yet. Connect once to download them."
                : null;
            return cached ?? [];
        }
    }

    public const string SwitchOffline = "You're offline. Connect to switch location.";
    public const string SwitchFailed = "Couldn't switch location. Try again.";

    public async Task<string?> SwitchOrgAsync(Guid orgNodeId)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/v1/auth/switch-org", new SwitchOrgRequest { OrgNodeId = orgNodeId });
            if (!response.IsSuccessStatusCode)
            {
                return SwitchFailed;
            }

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (body is null)
            {
                return SwitchFailed;
            }

            await tokenStore.SetTokenAsync(body.AccessToken, body.ExpiresAtUtc, body.DisplayName, body.CurrentLocationId, body.CurrentLocationName);
            return null;
        }
        catch (HttpRequestException)
        {
            // In the browser a request that never reached a server (no connection, DNS, CORS
            // preflight never answered) surfaces as this; a server that answered is handled above.
            return SwitchOffline;
        }
        catch (Exception)
        {
            return SwitchFailed;
        }
    }

    private async Task WriteCacheAsync(List<AssignedOrgDto> orgs)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvSet", StorageKey, JsonSerializer.Serialize(orgs, JsonOptions));
        }
        catch (Exception)
        {
            // A cache write failure must never break a working online session — the technician
            // simply won't have an offline copy from this load.
        }
    }

    private async Task<List<AssignedOrgDto>?> TryLoadFromCacheAsync()
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("dotGlassesIdb.kvGet", StorageKey);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<AssignedOrgDto>>(json, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

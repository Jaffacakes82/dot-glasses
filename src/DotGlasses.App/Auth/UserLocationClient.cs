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

    /// <summary>False on any failure (network, or the server rejecting a non-assigned org) —
    /// callers should treat that as "stayed on the previous location" and show a message.</summary>
    Task<bool> SwitchOrgAsync(Guid orgNodeId);

    /// <summary>True when the most recent GetMyOrgsAsync call served its result from the
    /// IndexedDB cache rather than a live API response — lets a caller tell "genuinely no orgs
    /// assigned" apart from "offline, showing the last-known list."</summary>
    bool IsFromCache { get; }

    /// <summary>True when the most recent GetMyOrgsAsync call's live request itself failed
    /// (network error, offline, expired token) — independent of IsFromCache, since a failed call
    /// with nothing cached yet still returns an empty list but is a different fact from
    /// "genuinely no orgs assigned" (a real 200 with an empty body).</summary>
    bool LastLoadFailed { get; }
}

public class UserLocationClient(HttpClient httpClient, AuthTokenStore tokenStore, IJSRuntime jsRuntime) : IUserLocationClient
{
    private const string StorageKey = "my-orgs-cache";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool IsFromCache { get; private set; }

    public bool LastLoadFailed { get; private set; }

    public async Task<IReadOnlyList<AssignedOrgDto>> GetMyOrgsAsync()
    {
        try
        {
            var orgs = await httpClient.GetFromJsonAsync<List<AssignedOrgDto>>("api/v1/auth/my-orgs") ?? [];
            IsFromCache = false;
            LastLoadFailed = false;
            await WriteCacheAsync(orgs);
            return orgs;
        }
        catch (Exception)
        {
            // Unreachable, offline, or the token has expired — fall back to the last-cached copy
            // rather than reporting an empty, unassigned-looking list.
            var cached = await TryLoadFromCacheAsync();
            IsFromCache = cached is not null;
            LastLoadFailed = true;
            return cached ?? [];
        }
    }

    public async Task<bool> SwitchOrgAsync(Guid orgNodeId)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/v1/auth/switch-org", new SwitchOrgRequest { OrgNodeId = orgNodeId });
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (body is null)
            {
                return false;
            }

            await tokenStore.SetTokenAsync(body.AccessToken, body.ExpiresAtUtc, body.DisplayName);
            return true;
        }
        catch (Exception)
        {
            return false;
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

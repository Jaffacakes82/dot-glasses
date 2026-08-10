using System.Net.Http.Json;
using DotGlasses.Contracts.Auth;

namespace DotGlasses.App.Auth;

/// <summary>
/// The technician's own assignable selling points (UserOrgAssignment) and the ability to switch
/// the active one — backs Settings.razor's location list and OutletSelect.razor's post-login
/// picker. Switching re-issues the JWT server-side (new HierarchyPath/OrgNodeId/OrgLevel claims),
/// so SwitchOrgAsync always writes the fresh token into AuthTokenStore itself on success — a
/// caller that forgot to do this would keep stamping new records under the *old* location.
/// </summary>
public interface IUserLocationClient
{
    Task<IReadOnlyList<AssignedOrgDto>> GetMyOrgsAsync();

    /// <summary>False on any failure (network, or the server rejecting a non-assigned org) —
    /// callers should treat that as "stayed on the previous location" and show a message.</summary>
    Task<bool> SwitchOrgAsync(Guid orgNodeId);
}

public class UserLocationClient(HttpClient httpClient, AuthTokenStore tokenStore) : IUserLocationClient
{
    public async Task<IReadOnlyList<AssignedOrgDto>> GetMyOrgsAsync()
    {
        try
        {
            var orgs = await httpClient.GetFromJsonAsync<List<AssignedOrgDto>>("api/v1/auth/my-orgs");
            return orgs ?? [];
        }
        catch (Exception)
        {
            // Unreachable, offline, or the token has expired — the caller falls back to showing
            // nothing switchable rather than a broken picker.
            return [];
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

            await tokenStore.SetTokenAsync(body.AccessToken, body.ExpiresAtUtc);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

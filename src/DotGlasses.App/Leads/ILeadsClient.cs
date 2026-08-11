using System.Net;
using System.Net.Http.Json;
using DotGlasses.Contracts.Leads;

namespace DotGlasses.App.Leads;

/// <summary>
/// Backs the Field App's leads worklist (ListOpenAsync), Lead→Sale prefill (GetByIdAsync), and
/// the automatic conversion-match prompt on a fresh Sale (FindOpenMatchAsync). All three fail soft
/// (empty list / null) rather than throwing — a technician offline or between requests should
/// still be able to record a Sale normally, not get blocked by a lookup that can't reach the
/// server.
/// </summary>
public interface ILeadsClient
{
    Task<IReadOnlyList<LeadDto>> ListOpenAsync();

    Task<LeadDto?> GetByIdAsync(Guid id);

    /// <summary>Null if there's no open Lead for this exact name+phone.</summary>
    Task<LeadDto?> FindOpenMatchAsync(string fullName, string? phoneNumber);
}

public class LeadsClient(HttpClient httpClient) : ILeadsClient
{
    public async Task<IReadOnlyList<LeadDto>> ListOpenAsync()
    {
        try
        {
            var leads = await httpClient.GetFromJsonAsync<List<LeadDto>>("api/v1/leads/open");
            return leads ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task<LeadDto?> GetByIdAsync(Guid id)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<LeadDto>($"api/v1/leads/{id}");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<LeadDto?> FindOpenMatchAsync(string fullName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        try
        {
            var query = $"api/v1/leads/match?fullName={Uri.EscapeDataString(fullName)}";
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                query += $"&phoneNumber={Uri.EscapeDataString(phoneNumber)}";
            }

            var response = await httpClient.GetAsync(query);
            if (response.StatusCode == HttpStatusCode.NoContent || !response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LeadDto>();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

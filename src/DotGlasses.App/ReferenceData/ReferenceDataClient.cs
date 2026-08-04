using System.Net.Http.Json;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;

namespace DotGlasses.App.ReferenceData;

public class ReferenceDataClient(HttpClient httpClient) : IReferenceDataClient
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<ReferenceDataItemDto> _items = [];

    public bool IsLoaded { get; private set; }
    public string? LoadError { get; private set; }
    public IReadOnlyList<PresetCatalogueDto> Catalogues { get; private set; } = [];

    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (IsLoaded)
            {
                return;
            }

            var items = await httpClient.GetFromJsonAsync<List<ReferenceDataItemDto>>("api/v1/reference-data");
            var catalogues = await httpClient.GetFromJsonAsync<List<PresetCatalogueDto>>("api/v1/preset-catalogues");

            _items = items ?? [];
            Catalogues = catalogues ?? [];
            LoadError = null;
            IsLoaded = true;
        }
        catch (Exception)
        {
            LoadError = "Couldn't reach the server to load lens/coating/frame options — this requires a connection the first time. Try again once you're back online.";
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<ReferenceDataItemDto> GetByCategory(ReferenceDataCategory category) =>
        _items.Where(x => x.Category == category).ToList();
}

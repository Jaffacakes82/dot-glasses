using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;
using Microsoft.JSInterop;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// Fetch-and-cache: a successful load is written through to IndexedDB, and a failed load falls
/// back to whatever was cached last. Before this, reference data was fetched once per session
/// with no persistence, so a technician who hadn't been online since the app started couldn't
/// open a consultation form at all — the forms rendered "Couldn't reach the server" with a Retry
/// button and nothing else. That was the single largest hole in the offline story after token
/// persistence.
/// </summary>
public class ReferenceDataClient(HttpClient httpClient, IJSRuntime jsRuntime) : IReferenceDataClient
{
    private const string StorageKey = "reference-data-cache";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<ReferenceDataItemDto> _items = [];

    public bool IsLoaded { get; private set; }

    public string? LoadError { get; private set; }

    public bool IsFromCache { get; private set; }

    public DateTimeOffset? CachedAtUtc { get; private set; }

    public IReadOnlyList<PresetCatalogueDto> Catalogues { get; private set; } = [];

    public IReadOnlyList<CoatingPairingDto> CoatingPairings { get; private set; } = [];

    public IReadOnlyList<CoatingExclusionDto> CoatingExclusions { get; private set; } = [];

    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded)
        {
            return;
        }

        await LoadAsync();
    }

    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            try
            {
                var items = await httpClient.GetFromJsonAsync<List<ReferenceDataItemDto>>("api/v1/reference-data");
                var catalogues = await httpClient.GetFromJsonAsync<List<PresetCatalogueDto>>("api/v1/preset-catalogues");
                var coatingRules = await httpClient.GetFromJsonAsync<CoatingRulesDto>("api/v1/reference-data/coating-rules");

                _items = items ?? [];
                Catalogues = catalogues ?? [];
                CoatingPairings = coatingRules?.Pairings ?? [];
                CoatingExclusions = coatingRules?.Exclusions ?? [];
                LoadError = null;
                IsFromCache = false;
                CachedAtUtc = null;
                IsLoaded = true;

                await WriteCacheAsync();
                return;
            }
            catch (Exception)
            {
                // Unreachable, offline, or the token has expired — fall through to the cache.
            }

            if (await TryLoadFromCacheAsync())
            {
                return;
            }

            LoadError = "Couldn't reach the server to load lens/coating/frame options, and this "
                + "device has no saved copy yet. Connect once to download them — after that they "
                + "stay available offline.";
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteCacheAsync()
    {
        var payload = new CachedPayload(DateTimeOffset.UtcNow, _items, Catalogues.ToList(), CoatingPairings.ToList(), CoatingExclusions.ToList());
        try
        {
            await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvSet", StorageKey, JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch (Exception)
        {
            // A cache write failure must never break a working online session — the technician
            // simply won't have an offline copy from this load.
        }
    }

    private async Task<bool> TryLoadFromCacheAsync()
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("dotGlassesIdb.kvGet", StorageKey);
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            var payload = JsonSerializer.Deserialize<CachedPayload>(json, JsonOptions);
            if (payload is null)
            {
                return false;
            }

            _items = payload.Items;
            Catalogues = payload.Catalogues;
            CoatingPairings = payload.CoatingPairings;
            CoatingExclusions = payload.CoatingExclusions;
            IsFromCache = true;
            CachedAtUtc = payload.CachedAtUtc;
            LoadError = null;
            IsLoaded = true;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public IReadOnlyList<ReferenceDataItemDto> GetByCategory(ReferenceDataCategory category) =>
        _items.Where(x => x.Category == category).ToList();

    /// <summary>CoatingPairings/CoatingExclusions default to an empty list so a cache payload
    /// written before those fields existed still deserializes safely (missing JSON properties
    /// fall back to the constructor's default parameter value).</summary>
    private sealed record CachedPayload(
        DateTimeOffset CachedAtUtc,
        List<ReferenceDataItemDto> Items,
        List<PresetCatalogueDto> Catalogues,
        List<CoatingPairingDto> CoatingPairings = null!,
        List<CoatingExclusionDto> CoatingExclusions = null!)
    {
        public List<CoatingPairingDto> CoatingPairings { get; init; } = CoatingPairings ?? [];
        public List<CoatingExclusionDto> CoatingExclusions { get; init; } = CoatingExclusions ?? [];
    }
}

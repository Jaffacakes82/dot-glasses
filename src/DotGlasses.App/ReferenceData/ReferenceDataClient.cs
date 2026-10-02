using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.App.Auth;
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
public class ReferenceDataClient : IReferenceDataClient
{
    private const string StorageKey = "reference-data-cache";

    /// <summary>The shape of the lens sets in the cache. 1 was ADR-0007's (each lens a lens power
    /// with its own coatings and pairings); 2 adds whose they are — the lens sets in a payload are
    /// the ones offered at <see cref="CachedPayload.LocationId"/> and nowhere else. A payload with
    /// no such field was written before either.</summary>
    private const int LensSetShape = 2;

    /// <summary>How long a refresh may keep a form waiting when a copy is already held in memory.
    /// A dead connection fails at once and never gets here; this bounds the connection that is up
    /// but going nowhere, where the alternative is the form sitting on "Loading" for HttpClient's
    /// own 100 seconds to fetch options the technician already has.</summary>
    private static readonly TimeSpan HeldCopyRefreshTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The same bound when nothing is held in memory yet (first form after a launch, a
    /// sign-in or a location switch). Longer, because the only fallback is the IndexedDB copy,
    /// which may be older or belong to another location — but still far short of HttpClient's 100
    /// seconds on a connection that is up and going nowhere.</summary>
    private static readonly TimeSpan FirstLoadTimeout = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly AuthTokenStore _tokenStore;
    private readonly FramePictureCache _framePictures;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<ReferenceDataItemDto> _items = [];

    /// <summary>Counts sign-ins, sign-outs and location switches. A load that finds it changed
    /// while it was waiting was answered for a session that is gone, and starts again.</summary>
    private int _session;

    public ReferenceDataClient(HttpClient httpClient, IJSRuntime jsRuntime, AuthTokenStore tokenStore, FramePictureCache framePictures)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _tokenStore = tokenStore;
        _framePictures = framePictures;

        // Both live for the whole app (singletons), so there is nothing to unsubscribe.
        _tokenStore.Changed += Invalidate;
    }

    public bool IsLoaded { get; private set; }

    public string? LoadError { get; private set; }

    public bool IsFromCache { get; private set; }

    public DateTimeOffset? CachedAtUtc { get; private set; }

    public IReadOnlyList<PresetCatalogueDto> Catalogues { get; private set; } = [];

    public IReadOnlyList<CoatingExclusionDto> CoatingExclusions { get; private set; } = [];

    public async Task RefreshAsync()
    {
        await _gate.WaitAsync();
        try
        {
            while (!await TryLoadForCurrentSessionAsync())
            {
                // Signed in, out or switched location mid-load — go again for the new session.
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Sign-in, sign-out and location switch all discard the in-memory copy: lens sets are scoped
    /// server-side to the token's current location, and a signed-out device must not keep the
    /// previous user's copy readable. The next form to open loads for whoever is signed in then.
    /// </summary>
    private void Invalidate()
    {
        _session++;
        _items = [];
        Catalogues = [];
        CoatingExclusions = [];
        IsLoaded = false;
        IsFromCache = false;
        CachedAtUtc = null;
        LoadError = null;
    }

    /// <summary>One attempt: a fresh fetch, else the copy already in memory, else the IndexedDB
    /// cache. False means the session changed while it waited and nothing it read may be used.</summary>
    private async Task<bool> TryLoadForCurrentSessionAsync()
    {
        var session = _session;
        var locationId = _tokenStore.CurrentLocationId;

        if (_tokenStore.AccessToken is null)
        {
            // Signed out: nothing to load for, and the cache must not refill what sign-out cleared.
            return true;
        }

        var fetched = await TryFetchAsync(IsLoaded ? HeldCopyRefreshTimeout : FirstLoadTimeout);
        if (session != _session)
        {
            return false;
        }

        if (fetched is not null)
        {
            _items = fetched.Items;
            Catalogues = fetched.Catalogues;
            CoatingExclusions = fetched.CoatingExclusions;
            LoadError = null;
            IsFromCache = false;
            CachedAtUtc = null;
            IsLoaded = true;

            await WriteCacheAsync(locationId);

            // The frame colour pictures follow the lists onto the device. Not awaited: a slow or
            // failed picture must never hold a form on "Loading options…" — the swatches show the
            // address meanwhile and the copy lands for next time.
            _ = _framePictures.SyncAsync(fetched.Items);

            // A sign-out or location switch during the write cleared what was just assigned.
            return session == _session;
        }

        if (IsLoaded)
        {
            // Unreachable, offline, or the token has expired — but this session already holds a
            // copy at least as new as the cache, so it stands.
            return true;
        }

        var cached = await TryReadCacheAsync();
        if (session != _session)
        {
            return false;
        }

        if (cached is not null)
        {
            await _framePictures.LoadStoredAsync();
            _items = cached.Items;
            Catalogues = LensSetsUsableAt(cached, locationId);
            CoatingExclusions = cached.CoatingExclusions;
            IsFromCache = true;
            CachedAtUtc = cached.CachedAtUtc;
            LoadError = null;
            IsLoaded = true;
            return true;
        }

        LoadError = "Couldn't reach the server to load lens/coating/frame options, and this "
            + "device has no saved copy yet. Connect once to download them — after that they "
            + "stay available offline.";
        return true;
    }

    /// <summary>
    /// Which of a cached payload's lens sets may be offered at <paramref name="locationId"/>: all
    /// of them when the payload was written at that location in the current shape, otherwise none.
    /// Reference items and exclusions are one global library and are used whatever this says.
    ///
    /// Two kinds of payload lose their lens sets, and in both the technician sees "no lens sets"
    /// until they are next online, which replaces the whole payload. One was written at another
    /// retail point: lens sets are assigned per location, and offering the last location's would
    /// let a technician record against a set the server then refuses. The other is older than the
    /// current shape: before shape 1 a lens was a label and nothing else, so each would read as
    /// sphere 0.00 with no coatings and choosing one would record a prescription nobody made, and
    /// shape 1 doesn't say which location its lens sets were for.
    /// </summary>
    private static IReadOnlyList<PresetCatalogueDto> LensSetsUsableAt(CachedPayload payload, Guid? locationId) =>
        payload.LensSetShape >= LensSetShape && locationId is not null && payload.LocationId == locationId
            ? payload.Catalogues ?? []
            : [];

    private async Task<Fetched?> TryFetchAsync(TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            var items = await _httpClient.GetFromJsonAsync<List<ReferenceDataItemDto>>("api/v1/reference-data", cancellation.Token);
            var catalogues = await _httpClient.GetFromJsonAsync<List<PresetCatalogueDto>>("api/v1/preset-catalogues", cancellation.Token);
            var coatingRules = await _httpClient.GetFromJsonAsync<CoatingRulesDto>("api/v1/reference-data/coating-rules", cancellation.Token);

            return new Fetched(items ?? [], catalogues ?? [], coatingRules?.Exclusions ?? []);
        }
        catch (Exception)
        {
            // Unreachable, offline, too slow, or the token has expired — the caller falls back.
            return null;
        }
    }

    private async Task WriteCacheAsync(Guid? locationId)
    {
        var payload = new CachedPayload(
            DateTimeOffset.UtcNow, _items, Catalogues.ToList(), CoatingExclusions.ToList(), LensSetShape, locationId);
        try
        {
            await _jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvSet", StorageKey, JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch (Exception)
        {
            // A cache write failure must never break a working online session — the technician
            // simply won't have an offline copy from this load.
        }
    }

    private async Task<CachedPayload?> TryReadCacheAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("dotGlassesIdb.kvGet", StorageKey);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CachedPayload>(json, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public IReadOnlyList<ReferenceDataItemDto> GetByCategory(ReferenceDataCategory category) =>
        _items.Where(x => x.Category == category).ToList();

    public IReadOnlyList<ReferenceDataItemDto> AllItems => _items;

    public string? PictureSource(ReferenceDataItemDto item) => _framePictures.SourceFor(item);

    /// <summary>
    /// CoatingExclusions defaults to an empty list so a cache payload written before that field
    /// existed still deserializes safely (missing JSON properties fall back to the constructor's
    /// default parameter value).
    ///
    /// Older payloads are read, never rejected, but their lens sets are not used: LensSetShape
    /// and LocationId are both absent from a payload written before they existed (so 0 and null),
    /// and <see cref="LensSetsUsableAt"/> drops the lens sets of any payload it can't place. Its
    /// reference items and exclusions are still used, and the next online load replaces the whole
    /// payload.
    /// </summary>
    private sealed record CachedPayload(
        DateTimeOffset CachedAtUtc,
        List<ReferenceDataItemDto> Items,
        List<PresetCatalogueDto> Catalogues,
        List<CoatingExclusionDto> CoatingExclusions = null!,
        int LensSetShape = 0,
        Guid? LocationId = null)
    {
        public List<CoatingExclusionDto> CoatingExclusions { get; init; } = CoatingExclusions ?? [];
    }

    private sealed record Fetched(
        List<ReferenceDataItemDto> Items, List<PresetCatalogueDto> Catalogues, List<CoatingExclusionDto> CoatingExclusions);
}

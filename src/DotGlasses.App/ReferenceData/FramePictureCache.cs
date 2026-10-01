using System.Text.Json;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.ReferenceData;
using Microsoft.JSInterop;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// Copies of the frame colour pictures kept on the device, so the Sale form's swatches show with
/// no connection. Write-through like the cached lists: after a successful reference-data load the
/// pictures the lists point at are fetched and stored in IndexedDB; offline, the stored copies are
/// what the swatches render.
///
/// Only pictures the Admin Portal serves are copied — an item's ImageUrl that is a path, resolved
/// against the API address. An older item still pointing at another website is left as a plain
/// web address: fetching it would send this app's HttpClient (and its bearer token) to someone
/// else's server, and that site doesn't allow the copy anyway. Such a picture shows online and
/// falls back to the placeholder offline.
///
/// Nothing here may break a form: every failure leaves the picture uncopied and is otherwise
/// ignored.
/// </summary>
public class FramePictureCache(HttpClient httpClient, IJSRuntime jsRuntime)
{
    private const string StorageKey = "frame-pictures-cache";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>ImageUrl → a data: URL holding the picture's bytes.</summary>
    private Dictionary<string, string> _pictures = [];
    private bool _loaded;

    /// <summary>What an &lt;img&gt; should load for this item: the stored copy when there is one,
    /// otherwise the picture's address (a path is resolved against the API address), or null when
    /// the item has no picture.</summary>
    public string? SourceFor(ReferenceDataItemDto item)
    {
        if (string.IsNullOrWhiteSpace(item.ImageUrl))
        {
            return null;
        }

        if (_pictures.TryGetValue(item.ImageUrl, out var stored))
        {
            return stored;
        }

        return IsServedByTheAdminPortal(item.ImageUrl) && httpClient.BaseAddress is { } apiBase
            ? new Uri(apiBase, item.ImageUrl).ToString()
            : item.ImageUrl;
    }

    /// <summary>Reads the stored copies into memory — what an offline form needs. Idempotent.</summary>
    public async Task LoadStoredAsync()
    {
        if (_loaded)
        {
            return;
        }

        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("dotGlassesIdb.kvGet", StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                _pictures = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];
            }
        }
        catch (Exception)
        {
            // No stored copies: the swatches fall back to the addresses, then the placeholder.
        }

        _loaded = true;
    }

    /// <summary>
    /// Brings the stored copies in line with the lists just loaded: fetches a picture not held
    /// yet, and drops any no list points at any more. A replaced picture has a new address (stored
    /// names never repeat), so it is fetched as a new one and the old copy goes.
    /// </summary>
    public async Task SyncAsync(IReadOnlyList<ReferenceDataItemDto> items)
    {
        await LoadStoredAsync();

        var wanted = items
            .Where(i => i.Category is ReferenceDataCategory.FrameColour or ReferenceDataCategory.FrameColourChild)
            .Select(i => i.ImageUrl)
            .Where(url => !string.IsNullOrWhiteSpace(url) && IsServedByTheAdminPortal(url!))
            .Select(url => url!)
            .Distinct()
            .ToList();

        var next = _pictures.Where(p => wanted.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        var changed = next.Count != _pictures.Count;

        foreach (var url in wanted.Where(url => !next.ContainsKey(url)))
        {
            if (await TryFetchAsync(url) is { } dataUrl)
            {
                next[url] = dataUrl;
                changed = true;
            }
        }

        _pictures = next;
        if (!changed)
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("dotGlassesIdb.kvSet", StorageKey, JsonSerializer.Serialize(next, JsonOptions));
        }
        catch (Exception)
        {
            // Held in memory for this session; there just won't be an offline copy from this load.
        }
    }

    private async Task<string?> TryFetchAsync(string path)
    {
        try
        {
            using var response = await httpClient.GetAsync(path);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || !contentType.StartsWith("image/", StringComparison.Ordinal))
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A path on the API's own host, as opposed to a full address on another website.</summary>
    private static bool IsServedByTheAdminPortal(string imageUrl) =>
        imageUrl.StartsWith('/') && !imageUrl.StartsWith("//", StringComparison.Ordinal);
}

using System.Text.Json;
using System.Text.Json.Serialization;
using DotGlasses.Contracts.Sync;
using Microsoft.JSInterop;

namespace DotGlasses.App.Sync;

/// <summary>
/// ISyncQueueStore backed by IndexedDB (wwwroot/js/idbInterop.js) — every offline-created
/// record is written here, with a client-generated GUID and PendingSync status, before any
/// network call is attempted (brief 3.6's outbox pattern).
/// </summary>
public class IndexedDbOutboxStore(IJSRuntime jsRuntime) : ISyncQueueStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task EnqueueAsync(OutboxItem item, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(item, JsonOptions);
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.enqueue", cancellationToken, json);
    }

    public async Task<IReadOnlyList<OutboxItem>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var json = await jsRuntime.InvokeAsync<string>("dotGlassesIdb.getPending", cancellationToken);
        return JsonSerializer.Deserialize<List<OutboxItem>>(json, JsonOptions) ?? [];
    }

    public async Task<IReadOnlyList<OutboxItem>> GetFailedAsync(CancellationToken cancellationToken = default)
    {
        var json = await jsRuntime.InvokeAsync<string>("dotGlassesIdb.getFailed", cancellationToken);
        return JsonSerializer.Deserialize<List<OutboxItem>>(json, JsonOptions) ?? [];
    }

    public async Task MarkSyncedAsync(Guid id, CancellationToken cancellationToken = default) =>
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.updateStatus", cancellationToken, id.ToString(), nameof(OutboxItemStatus.Synced), null);

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default) =>
        await jsRuntime.InvokeVoidAsync("dotGlassesIdb.updateStatus", cancellationToken, id.ToString(), nameof(OutboxItemStatus.Failed), error);
}

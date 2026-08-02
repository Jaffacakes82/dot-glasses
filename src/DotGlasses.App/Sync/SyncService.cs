using System.Net;
using System.Text;
using DotGlasses.Contracts.Sync;

namespace DotGlasses.App.Sync;

/// <summary>
/// Drains the outbox against the API, using each item's client-generated Id as the idempotency
/// key — DotGlasses.Web's WidgetExample create endpoint treats create as an upsert keyed on
/// Id, so a retried sync of the same record is a no-op, not a duplicate.
/// </summary>
public class SyncService(ISyncQueueStore queueStore, HttpClient httpClient, ILogger<SyncService> logger) : ISyncService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            // A sync is already running (e.g. the reconnect event and the poll timer fired
            // close together) — let it finish rather than draining the queue twice.
            return;
        }

        try
        {
            var pending = await queueStore.GetPendingAsync(cancellationToken);
            foreach (var item in pending)
            {
                await SyncItemAsync(item, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SyncItemAsync(OutboxItem item, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new StringContent(item.PayloadJson, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(item.ApiRoute, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await queueStore.MarkSyncedAsync(item.Id, cancellationToken);
            }
            else if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Not retryable without user action (bad data, or needs a fresh login) — record
                // it as failed rather than retrying forever on a timer.
                await queueStore.MarkFailedAsync(item.Id, $"HTTP {(int)response.StatusCode}", cancellationToken);
            }
            else
            {
                logger.LogWarning("Sync of outbox item {ItemId} ({EntityType}) failed with {StatusCode}; will retry.", item.Id, item.EntityType, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sync of outbox item {ItemId} ({EntityType}) threw; will retry.", item.Id, item.EntityType);
        }
    }
}

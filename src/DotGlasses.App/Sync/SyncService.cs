using System.Net;
using System.Text;
using System.Text.Json;
using DotGlasses.Contracts.Sync;

namespace DotGlasses.App.Sync;

/// <summary>
/// Drains the outbox against the API, using each item's client-generated Id as the idempotency
/// key — every create endpoint treats a create as an upsert keyed on Id, so a retried sync of the
/// same record is a no-op, not a duplicate.
///
/// A rejection is parsed into real field-level messages rather than stored as a bare status code.
/// "HTTP 400" told a technician nothing they could act on and was the reason a mistyped record
/// became unrecoverable: they could see that something had failed but never what.
/// </summary>
public class SyncService(ISyncQueueStore queueStore, HttpClient httpClient, ILogger<SyncService> logger) : ISyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<SyncAttemptResult> TrySyncItemAsync(OutboxItem item, CancellationToken cancellationToken = default) =>
        await SyncItemAsync(item, cancellationToken);

    private async Task<SyncAttemptResult> SyncItemAsync(OutboxItem item, CancellationToken cancellationToken)
    {
        try
        {
            using var content = new StringContent(item.PayloadJson, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(item.ApiRoute, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await queueStore.MarkSyncedAsync(item.Id, cancellationToken);
                return SyncAttemptResult.Succeeded();
            }

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Not retryable without user action (bad data, or needs a fresh login) — record
                // it as failed rather than retrying forever on a timer.
                var rejection = await ReadRejectionAsync(response, cancellationToken);
                await queueStore.MarkFailedAsync(item.Id, rejection.Message ?? "Rejected by the server.", cancellationToken);
                return rejection;
            }

            logger.LogWarning("Sync of outbox item {ItemId} ({EntityType}) failed with {StatusCode}; will retry.", item.Id, item.EntityType, response.StatusCode);
            return SyncAttemptResult.Deferred();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sync of outbox item {ItemId} ({EntityType}) threw; will retry.", item.Id, item.EntityType);
            return SyncAttemptResult.Deferred();
        }
    }

    /// <summary>
    /// Turns an ASP.NET ValidationProblemDetails body into per-field messages. Falls back to the
    /// problem title, then to a generic message, so an unexpected error shape still produces
    /// something readable instead of an empty string.
    /// </summary>
    private static async Task<SyncAttemptResult> ReadRejectionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return SyncAttemptResult.Rejected(
                "Your session has expired or you don't have permission to save this. Sign in again and re-send.");
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var problem = JsonSerializer.Deserialize<ValidationProblemResponse>(body, JsonOptions);

            if (problem?.Errors is { Count: > 0 } errors)
            {
                var summary = string.Join(" ", errors.SelectMany(e => e.Value).Distinct());
                return SyncAttemptResult.Rejected(summary, errors);
            }

            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return SyncAttemptResult.Rejected(problem.Detail);
            }

            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                return SyncAttemptResult.Rejected(problem.Title);
            }
        }
        catch (Exception)
        {
            // Unparseable body — fall through to the generic message below.
        }

        return SyncAttemptResult.Rejected("The server rejected this record.");
    }

    private sealed class ValidationProblemResponse
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }

        public Dictionary<string, string[]>? Errors { get; set; }
    }
}

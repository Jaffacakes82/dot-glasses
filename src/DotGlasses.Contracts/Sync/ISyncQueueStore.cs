namespace DotGlasses.Contracts.Sync;

/// <summary>
/// Abstraction over the local (IndexedDB) outbox table. Implemented in DotGlasses.App —
/// defined here so DotGlasses.App.Tests-style consumers and DotGlasses.ISyncService can depend
/// on the shape without pulling in JS interop.
/// </summary>
public interface ISyncQueueStore
{
    Task EnqueueAsync(OutboxItem item, CancellationToken cancellationToken = default);

    /// <summary>Items still eligible for a retry (PendingSync/Syncing) — excludes both Synced
    /// and Failed. <see cref="ISyncService"/> drains exactly this set, so a permanently-failed
    /// item (see <see cref="MarkFailedAsync"/>) is never retried again once marked.</summary>
    Task<IReadOnlyList<OutboxItem>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Items that hit a permanent (non-retryable) failure — needs a technician's
    /// attention, not another sync attempt. Surfaced on the Field App's home screen.</summary>
    Task<IReadOnlyList<OutboxItem>> GetFailedAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches a single item regardless of status — used to reload a failed record's
    /// payload back into the form that created it, so a correction starts from what the
    /// technician actually entered rather than an empty form.</summary>
    Task<OutboxItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task MarkSyncedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary><paramref name="error"/> is a human-readable summary of why the server refused
    /// it, not a bare status code — it is shown verbatim to the technician on the failed-records
    /// screen, so it has to be something they can act on.</summary>
    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);

    /// <summary>Puts a corrected item back into the retry set with a replaced payload, keeping
    /// the same Id so the server's idempotent upsert still applies.</summary>
    Task RequeueAsync(Guid id, string payloadJson, CancellationToken cancellationToken = default);

    /// <summary>Discards an item outright — only ever used for a permanently-failed record the
    /// technician has chosen to abandon.</summary>
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}

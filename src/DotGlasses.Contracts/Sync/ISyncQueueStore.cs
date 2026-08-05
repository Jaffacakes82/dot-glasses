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

    Task MarkSyncedAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);
}

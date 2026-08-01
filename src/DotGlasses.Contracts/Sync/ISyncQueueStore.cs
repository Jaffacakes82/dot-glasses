namespace DotGlasses.Contracts.Sync;

/// <summary>
/// Abstraction over the local (IndexedDB) outbox table. Implemented in DotGlasses.App —
/// defined here so DotGlasses.App.Tests-style consumers and DotGlasses.ISyncService can depend
/// on the shape without pulling in JS interop.
/// </summary>
public interface ISyncQueueStore
{
    Task EnqueueAsync(OutboxItem item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxItem>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task MarkSyncedAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);
}

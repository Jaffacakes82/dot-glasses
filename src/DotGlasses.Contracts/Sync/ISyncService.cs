namespace DotGlasses.Contracts.Sync;

/// <summary>
/// Drains the offline outbox against the API when connectivity returns. Implemented in
/// DotGlasses.App.
/// </summary>
public interface ISyncService
{
    Task SyncPendingAsync(CancellationToken cancellationToken = default);
}

namespace DotGlasses.Contracts.Sync;

/// <summary>
/// Drains the offline outbox against the API when connectivity returns. Implemented in
/// DotGlasses.App.
/// </summary>
public interface ISyncService
{
    Task SyncPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes one specific item immediately and reports what happened, so the form that just
    /// created it can react — rendering field-level errors on a rejection, or navigating away
    /// when the record is safely queued. <see cref="SyncPendingAsync"/> deliberately reports
    /// nothing, because it drains a whole queue on a background timer with no user attached.
    /// </summary>
    Task<SyncAttemptResult> TrySyncItemAsync(OutboxItem item, CancellationToken cancellationToken = default);
}

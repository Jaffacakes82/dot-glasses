namespace DotGlasses.Contracts.Sync;

public enum OutboxItemStatus
{
    PendingSync,
    Syncing,
    Synced,
    Failed,
}

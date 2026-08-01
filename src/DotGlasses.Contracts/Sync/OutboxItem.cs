namespace DotGlasses.Contracts.Sync;

/// <summary>
/// A single queued offline write, persisted in the Field App's IndexedDB outbox before any
/// network call is attempted. <see cref="Id"/> is the client-generated GUID used as the
/// idempotency key when the sync service replays this item against the API.
/// </summary>
public class OutboxItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>e.g. "WidgetExample". Lets one outbox/sync mechanism carry any entity type,
    /// real domain entities included, once they exist.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Relative API route to call when draining this item, e.g. "api/v1/widget-examples".</summary>
    public string ApiRoute { get; set; } = string.Empty;

    /// <summary>The request DTO (e.g. CreateWidgetExampleRequest), serialized as JSON.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public OutboxItemStatus Status { get; set; } = OutboxItemStatus.PendingSync;

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? LastError { get; set; }
}

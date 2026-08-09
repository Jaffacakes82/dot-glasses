namespace DotGlasses.Contracts.Sync;

/// <summary>
/// Outcome of trying to push one outbox item to the API. Lets a form distinguish "the server
/// looked at this and said no" from "we couldn't reach the server" — the two need opposite
/// handling: a rejection must keep the technician on the pre-filled form with the offending
/// fields marked, while an unreachable server is the normal offline path and should just leave
/// the record queued and let them carry on.
/// </summary>
public enum SyncAttemptOutcome
{
    /// <summary>Accepted by the API and marked Synced.</summary>
    Succeeded,

    /// <summary>Couldn't reach the server, or the server failed transiently (5xx). The item stays
    /// queued and will be retried automatically — this is the expected offline case.</summary>
    Deferred,

    /// <summary>The server rejected the payload (400/401/403). Not retryable without a change to
    /// the data or the session, so the item is marked Failed.</summary>
    Rejected,
}

/// <summary>
/// <see cref="FieldErrors"/> carries the API's ValidationProblemDetails errors keyed by the
/// request property name (e.g. "ReasonNotPurchasedRefId"), so a form can render each message
/// against the control that produced it rather than showing a bare status code.
/// </summary>
public record SyncAttemptResult(
    SyncAttemptOutcome Outcome,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static SyncAttemptResult Succeeded() => new(SyncAttemptOutcome.Succeeded);

    public static SyncAttemptResult Deferred(string? message = null) => new(SyncAttemptOutcome.Deferred, message);

    public static SyncAttemptResult Rejected(string? message, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(SyncAttemptOutcome.Rejected, message, fieldErrors);
}

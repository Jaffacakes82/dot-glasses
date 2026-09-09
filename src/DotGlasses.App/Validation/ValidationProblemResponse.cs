namespace DotGlasses.App.Validation;

/// <summary>
/// The shape of an ASP.NET ValidationProblemDetails response body, as far as any client-side
/// caller needs it — shared by SyncService's outbox rejection handling and any screen that posts
/// directly to the API instead (e.g. Settings.razor's Change Password, which can't go through the
/// offline outbox since it needs an immediate answer against the live server).
/// </summary>
public class ValidationProblemResponse
{
    public string? Title { get; set; }

    public string? Detail { get; set; }

    public Dictionary<string, string[]>? Errors { get; set; }
}

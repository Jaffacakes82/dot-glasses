namespace DotGlasses.App.Auth;

/// <summary>
/// The Field App's sign-in and current-location gate, as a pure function so the whole policy is
/// readable in one place. <c>MainLayout</c> is the single caller: it asks on every navigation and
/// every token change, and renders the page only when the answer is "allow" (null).
///
/// The policy is fail-closed: a page needs a sign-in AND a current location unless it is listed
/// below, so a page added later is gated by default rather than open by default.
/// <list type="bullet">
/// <item>Login — open to everyone, never redirects.</item>
/// <item>Home ("") — needs a token only. Home owns the "does this session have a valid location"
/// decision (no-location vs outlet-select, plus its offline state), so a page that needs a
/// location but has none is sent to Home to be routed onward, rather than duplicating that
/// server-backed choice here. Home allows itself with a token, so there is no loop.</item>
/// <item>No-location, outlet-select, settings, failed-records, not-found — need a token
/// only. A technician with no location must still be able to pick one, send or discard queued
/// records, and sign out.</item>
/// <item>Everything else (the consultation forms, Leads, anything new) — token and location.</item>
/// </list>
/// Only the presence of <see cref="AuthTokenStore.CurrentLocationId"/> is checked, never its
/// validity against the server: that needs connectivity, and an offline technician with a
/// persisted token and location must keep working (Home re-validates it on launch when online).
/// </summary>
public static class FieldAppAccessGate
{
    public const string LoginPath = "login";
    public const string HomePath = "";

    private static readonly string[] TokenOnlyPaths =
        ["no-location", "outlet-select", "settings", "failed-records", "not-found"];

    /// <summary>Returns the base-relative path to redirect to, or null when the page may render.</summary>
    /// <param name="relativePath">Base-relative path (<c>NavigationManager.ToBaseRelativePath</c>);
    /// a query string or fragment is ignored.</param>
    public static string? RedirectFor(string relativePath, bool isAuthenticated, bool hasCurrentLocation)
    {
        var segment = FirstSegment(relativePath);

        if (segment == LoginPath)
        {
            return null;
        }

        if (!isAuthenticated)
        {
            return LoginPath;
        }

        if (hasCurrentLocation || segment == HomePath || Array.IndexOf(TokenOnlyPaths, segment) >= 0)
        {
            return null;
        }

        return HomePath;
    }

    private static string FirstSegment(string relativePath)
    {
        var end = relativePath.IndexOfAny(['?', '#']);
        var path = (end < 0 ? relativePath : relativePath[..end]).Trim('/');
        var slash = path.IndexOf('/');
        return (slash < 0 ? path : path[..slash]).ToLowerInvariant();
    }
}

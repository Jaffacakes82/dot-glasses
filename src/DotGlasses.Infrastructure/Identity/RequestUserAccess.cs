using DotGlasses.Application.Common;
using Microsoft.AspNetCore.Http;

namespace DotGlasses.Infrastructure.Identity;

/// <summary>
/// The per-request memo of the signed-in user's <see cref="UserAccess"/>, kept in
/// HttpContext.Items. Items rather than a scoped service because DotGlassesDbContext's query
/// filter reads it: that context is pooled, so it can only reach per-request state through the
/// singleton IHttpContextAccessor (see the comment on its _httpContextAccessor field). Items
/// dies with the request, which is exactly the lifetime ADR-0006 asks for.
///
/// The memo is tagged with the user it was loaded for and only answers for that same user, so a
/// request whose principal changed after loading (a JWT replacing the cookie principal on an API
/// action, say) can never read someone else's scope — it reads UserAccess.None until its own
/// access is loaded.
/// </summary>
public static class RequestUserAccess
{
    private static readonly object Key = new();

    public static void Set(HttpContext httpContext, Guid userId, UserAccess access) =>
        httpContext.Items[Key] = new Entry(userId, access);

    public static UserAccess Get(HttpContext? httpContext)
    {
        if (httpContext?.Items[Key] is not Entry(var userId, var access))
        {
            return UserAccess.None;
        }

        return CurrentUserContext.ReadUserId(httpContext.User) == userId ? access : UserAccess.None;
    }

    private sealed record Entry(Guid UserId, UserAccess Access);
}

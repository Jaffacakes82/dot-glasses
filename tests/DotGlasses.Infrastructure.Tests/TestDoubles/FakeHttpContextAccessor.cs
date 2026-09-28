using System.Security.Claims;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;

namespace DotGlasses.Infrastructure.Tests.TestDoubles;

/// <summary>Builds an IHttpContextAccessor carrying what DotGlassesDbContext's global query filter
/// reads — mirrors how DotGlasses.Web actually populates it: the user's identity in the claims,
/// and their access (here, a scope of the one given path) memoised on the request, the way the
/// per-request recheck leaves it after reading the database (ADR-0006). hierarchyPathPrefix is
/// that one scope path; "" (or anything unparseable) means no scope at all.</summary>
public static class FakeHttpContextAccessor
{
    public static IHttpContextAccessor Create(bool isAuthenticated = true, string hierarchyPathPrefix = "", string userName = "test-user")
    {
        if (!isAuthenticated)
        {
            return new SimpleHttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };
        }

        var userId = Guid.NewGuid();
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
        ];
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        IReadOnlyList<HierarchyPath> scope = HierarchyPath.TryParse(hierarchyPathPrefix, out var path) ? [path] : [];
        RequestUserAccess.Set(httpContext, userId, new UserAccess(scope, null, null, false));

        return new SimpleHttpContextAccessor { HttpContext = httpContext };
    }

    /// <summary>
    /// A plain per-instance IHttpContextAccessor — deliberately NOT the real
    /// Microsoft.AspNetCore.Http.HttpContextAccessor class. That type's HttpContext property is
    /// backed by a single *static* AsyncLocal shared across every instance (by design — it's
    /// meant to be a singleton, with ASP.NET Core's own pipeline setting it once per real
    /// request). Constructing several real HttpContextAccessor instances and setting
    /// .HttpContext on each — as an earlier version of this fake did — makes them all clobber
    /// the same shared slot instead of holding independent state, which broke tests that keep
    /// two fake "current users" alive at once. This minimal reimplementation has no such
    /// shared state, so each instance is genuinely independent.
    /// </summary>
    private class SimpleHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}

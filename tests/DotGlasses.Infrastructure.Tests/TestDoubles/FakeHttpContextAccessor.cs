using System.Security.Claims;
using DotGlasses.Application.Common;
using Microsoft.AspNetCore.Http;

namespace DotGlasses.Infrastructure.Tests.TestDoubles;

/// <summary>Builds an IHttpContextAccessor carrying the claims DotGlassesDbContext's global
/// query filter reads — mirrors how DotGlasses.Web actually populates them at sign-in.</summary>
public static class FakeHttpContextAccessor
{
    public static IHttpContextAccessor Create(bool isAuthenticated = true, string hierarchyPathPrefix = "", string userName = "test-user")
    {
        if (!isAuthenticated)
        {
            return new SimpleHttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } };
        }

        List<Claim> claims =
        [
            new(ClaimTypes.Name, userName),
            new(DotGlassesClaimTypes.HierarchyPath, hierarchyPathPrefix),
        ];
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");

        return new SimpleHttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
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

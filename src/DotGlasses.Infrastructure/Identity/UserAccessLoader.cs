using System.Security.Claims;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Identity;

public class UserAccessLoader(DotGlassesDbContext dbContext, IHttpContextAccessor httpContextAccessor) : IUserAccessLoader
{
    public async Task<UserAccess?> LoadForAdminPortalAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (CurrentUserContext.ReadUserId(principal) is not { } userId || await ReadAsync(userId, cancellationToken) is not { } row)
        {
            return null;
        }

        return Remember(userId, FromAssignments(row));
    }

    public async Task<UserAccess?> LoadForFieldAppAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (CurrentUserContext.ReadUserId(principal) is not { } userId || await ReadAsync(userId, cancellationToken) is not { } row)
        {
            return null;
        }

        var tokenOrg = HierarchyPath.TryParse(principal.FindFirstValue(DotGlassesClaimTypes.HierarchyPath), out var path)
            ? [path]
            : Array.Empty<HierarchyPath>();

        // Level, role and suspension exactly as the Admin Portal reads them; only the scope differs.
        return Remember(userId, FromAssignments(row) with { ScopePaths = tokenOrg });
    }

    /// <summary>One round trip: the account's lockout, its role and the orgs it is assigned to.
    /// IgnoreQueryFilters is load-bearing twice over. The org nodes must be read unscoped — an
    /// assignment is exactly what *defines* the scope, so filtering it by the scope would be
    /// circular — and ignoring the filters means their parameters are never evaluated, so this
    /// query can run before the request's scope exists. Soft-deleted orgs are excluded by hand in
    /// its place.
    ///
    /// Transitional: the account's old active org (ApplicationUser.OrgNodeId) counts as one of
    /// its assignments, so no one loses access before the migration that removes that column
    /// backfills an assignment row for it.</summary>
    private async Task<AccessRow?> ReadAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new AccessRow(
                u.LockoutEnd,
                dbContext.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
                    .OrderBy(name => name)
                    .FirstOrDefault(),
                dbContext.OrganisationNodes
                    .Where(o => !o.IsDeleted &&
                        (o.Id == u.OrgNodeId || dbContext.UserOrgAssignments.Any(a => a.UserId == u.Id && a.OrgNodeId == o.Id)))
                    .Select(o => new AccessOrg(o.HierarchyPath, o.Level))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    private static UserAccess FromAssignments(AccessRow row) => UserAccess.FromAssignments(
        row.Orgs.Select(o => (HierarchyPath.Parse(o.HierarchyPath), o.Level)),
        row.Role,
        UserSuspension.IsSuspended(row.LockoutEnd));

    private UserAccess Remember(Guid userId, UserAccess access)
    {
        if (httpContextAccessor.HttpContext is { } httpContext)
        {
            RequestUserAccess.Set(httpContext, userId, access);
        }

        return access;
    }

    private sealed record AccessRow(DateTimeOffset? LockoutEnd, string? Role, List<AccessOrg> Orgs);

    private sealed record AccessOrg(string HierarchyPath, OrganisationLevel Level);
}

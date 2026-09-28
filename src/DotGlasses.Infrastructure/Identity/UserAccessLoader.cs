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
        if (CurrentUserContext.ReadUserId(principal) is not { } userId || await ReadAsync(userId, null, cancellationToken) is not { } row)
        {
            return null;
        }

        return Remember(userId, FromAssignments(row));
    }

    public async Task<UserAccess?> LoadForFieldAppAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var locationId = Guid.TryParse(principal.FindFirstValue(DotGlassesClaimTypes.CurrentLocationId), out var id) ? id : (Guid?)null;

        if (CurrentUserContext.ReadUserId(principal) is not { } userId || await ReadAsync(userId, locationId, cancellationToken) is not { } row)
        {
            return null;
        }

        var location = CurrentLocationCheck.Of(row.Orgs.Where(o => o.Id == locationId).Select(ToCandidate).FirstOrDefault());

        // Level, role and suspension exactly as the Admin Portal reads them; only the scope differs.
        return Remember(userId, FromAssignments(row) with
        {
            ScopePaths = location.ValidLocation is { } valid ? [valid.Path] : [],
            CurrentLocation = location,
        });
    }

    public async Task<IReadOnlyList<CurrentLocation>> ListEligibleLocationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await ReadAsync(userId, null, cancellationToken) is not { } row)
        {
            return [];
        }

        return row.Orgs
            .Select(o => CurrentLocationCheck.Of(ToCandidate(o)).ValidLocation)
            .OfType<CurrentLocation>()
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>One round trip: the account's lockout, its role, the orgs it is assigned to and —
    /// assigned or not — the org <paramref name="locationId"/> names, so a location the user has
    /// lost can still be named when it is refused. IgnoreQueryFilters is load-bearing twice over.
    /// The org nodes must be read unscoped — an assignment is exactly what *defines* the scope, so
    /// filtering it by the scope would be circular — and ignoring the filters means their
    /// parameters are never evaluated, so this query can run before the request's scope exists.
    /// It also keeps deactivated (soft-deleted) orgs, which the scope leaves out by hand and the
    /// current-location check reports as deactivated.
    ///
    /// The UserOrgAssignment rows are the only source of access: an account with none gets an
    /// empty scope, no level and no eligible location — fail closed.</summary>
    private async Task<AccessRow?> ReadAsync(Guid userId, Guid? locationId, CancellationToken cancellationToken) =>
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
                    .Select(o => new
                    {
                        Org = o,
                        IsAssigned = dbContext.UserOrgAssignments.Any(a => a.UserId == u.Id && a.OrgNodeId == o.Id),
                    })
                    .Where(x => x.IsAssigned || x.Org.Id == locationId)
                    .Select(x => new AccessOrg(x.Org.Id, x.Org.HierarchyPath, x.Org.Name, x.Org.Level, x.Org.IsDeleted, x.IsAssigned))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    private static UserAccess FromAssignments(AccessRow row) => UserAccess.FromAssignments(
        row.Orgs.Where(o => o.IsAssigned && !o.IsDeleted).Select(o => (HierarchyPath.Parse(o.HierarchyPath), o.Level)),
        row.Role,
        UserSuspension.IsSuspended(row.LockoutEnd));

    private static LocationCandidate ToCandidate(AccessOrg org) =>
        new(org.Id, HierarchyPath.Parse(org.HierarchyPath), org.Name, org.Level, IsActive: !org.IsDeleted, IsDirectlyAssigned: org.IsAssigned);

    private UserAccess Remember(Guid userId, UserAccess access)
    {
        if (httpContextAccessor.HttpContext is { } httpContext)
        {
            RequestUserAccess.Set(httpContext, userId, access);
        }

        return access;
    }

    private sealed record AccessRow(DateTimeOffset? LockoutEnd, string? Role, List<AccessOrg> Orgs);

    private sealed record AccessOrg(Guid Id, string HierarchyPath, string Name, OrganisationLevel Level, bool IsDeleted, bool IsAssigned);
}

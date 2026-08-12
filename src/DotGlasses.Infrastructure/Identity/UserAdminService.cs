using DotGlasses.Application.Common;
using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Identity;

public class UserAdminService(UserManager<ApplicationUser> userManager, DotGlassesDbContext dbContext, ICurrentUserContext currentUser) : IUserAdminService
{
    public async Task<IReadOnlyList<UserAdminRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        var prefix = currentUser.HierarchyPathPrefix;
        var users = await userManager.Users
            .Where(u => u.HierarchyPath.StartsWith(prefix))
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.Id).ToList();

        var salesCounts = await dbContext.Sales
            .Where(s => userIds.Contains(s.TechnicianUserId))
            .GroupBy(s => s.TechnicianUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var assignments = await dbContext.UserOrgAssignments
            .Where(a => userIds.Contains(a.UserId))
            .ToListAsync(cancellationToken);

        // Scoped automatically (OrganisationNode implements IHierarchyScoped) — an org an
        // assigned-to user has that falls outside the caller's own scope (rare: a DGI-assigned
        // user with a foreign secondary org, viewed by a narrower-scoped caller) resolves to
        // "Unknown" via the fallback below rather than throwing.
        var orgNames = await dbContext.OrganisationNodes.ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        var rows = new List<UserAdminRow>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var userAssignments = assignments.Where(a => a.UserId == user.Id).ToList();
            var assignedOrgNames = userAssignments.Select(a => orgNames.GetValueOrDefault(a.OrgNodeId, "Unknown")).ToList();
            var assignedOrgIds = userAssignments.Select(a => a.OrgNodeId).ToList();

            rows.Add(new UserAdminRow(
                user.Id,
                user.Email ?? user.UserName ?? "—",
                string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? "—" : user.FullName,
                roles.FirstOrDefault() ?? "—",
                assignedOrgNames,
                assignedOrgIds,
                user.OrgNodeId,
                ResolveStatus(user),
                user.LastLoginUtc,
                salesCounts.GetValueOrDefault(user.Id, 0),
                user.HierarchyPath));
        }

        return rows;
    }

    public async Task<PagedResult<UserAdminRow>> ListPagedAsync(string? search, string? role, string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Reuses ListAsync (correct hierarchy-prefix scoping + role resolution already lives
        // there) rather than duplicating it — filters/pages the already-materialized result
        // instead of pushing to SQL, since role/status aren't queryable columns (role lives in
        // AspNetUserRoles, status is derived from PasswordHash/LockoutEnd) and the underlying
        // scoped user count is already small enough that ListAsync loads it all into memory today.
        IEnumerable<UserAdminRow> filtered = await ListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(u =>
                u.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            filtered = filtered.Where(u => u.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(u => u.Status == status);
        }

        var filteredList = filtered.ToList();
        var items = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<UserAdminRow>(items, filteredList.Count, page, pageSize);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        await userManager.FindByEmailAsync(email) is not null;

    public async Task<InviteUserResult> InviteAsync(string email, string fullName, string role, IReadOnlyList<Guid> orgNodeIds, CancellationToken cancellationToken = default)
    {
        var primaryOrg = await dbContext.OrganisationNodes.FirstAsync(o => o.Id == orgNodeIds[0], cancellationToken);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = fullName,
            OrgNodeId = primaryOrg.Id,
            HierarchyPath = primaryOrg.HierarchyPath,
            OrgLevel = primaryOrg.Level,
        };

        // No password — the account stays in the "Invited" state (PasswordHash is null) until
        // the user completes the set-password link.
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, role);

        foreach (var orgNodeId in orgNodeIds)
        {
            dbContext.UserOrgAssignments.Add(new UserOrgAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OrgNodeId = orgNodeId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return new InviteUserResult(user.Id, email, token);
    }

    public async Task<string> RegeneratePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        return await userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task SuspendAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }

    public async Task UnsuspendAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        await userManager.SetLockoutEndDateAsync(user, null);
    }

    public async Task AssignUserToOrgAsync(Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var alreadyAssigned = await dbContext.UserOrgAssignments
            .AnyAsync(a => a.UserId == userId && a.OrgNodeId == orgNodeId, cancellationToken);
        if (alreadyAssigned)
        {
            return;
        }

        dbContext.UserOrgAssignments.Add(new UserOrgAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrgNodeId = orgNodeId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UnassignUserFromOrgAsync(Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        if (user.OrgNodeId == orgNodeId)
        {
            throw new InvalidOperationException("Can't un-assign a user's primary org — switch their primary org first.");
        }

        var entity = await dbContext.UserOrgAssignments
            .FirstOrDefaultAsync(a => a.UserId == userId && a.OrgNodeId == orgNodeId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.UserOrgAssignments.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveStatus(ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return "Invited";
        }

        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow)
        {
            return "Suspended";
        }

        return "Active";
    }
}

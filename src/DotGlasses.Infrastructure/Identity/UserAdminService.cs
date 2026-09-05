using DotGlasses.Application.Common;
using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Common;
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

    /// <summary>
    /// The account, its role and its org assignments are one unit of work. They used to be three
    /// independent writes that each committed on their own, so a failure part-way through left a
    /// user with no role, or no location, or both — a state User Directory then had to render and
    /// an admin had to unpick by hand.
    ///
    /// This is a genuine database transaction, not a compensating "delete the user I just made"
    /// path, and it only works because Identity and the assignment writes share one DbContext:
    /// DotGlassesDbContext *is* the IdentityDbContext, and Program.cs's
    /// AddEntityFrameworkStores&lt;DotGlassesDbContext&gt; hands UserStore the same scoped
    /// instance this service holds. UserManager calls SaveChanges internally on every operation,
    /// so enrolling that shared context in an explicit transaction is the only thing that batches
    /// them. InviteAtomicityTests asserts both halves against real Postgres — that a UserManager
    /// write really does enrol in a transaction opened here, and that a failure at any of the
    /// three steps leaves nothing behind.
    /// </summary>
    public async Task<InviteUserResult> InviteAsync(string email, string fullName, string role, IReadOnlyList<Guid> orgNodeIds, CancellationToken cancellationToken = default)
    {
        // Routed through the execution strategy rather than calling BeginTransactionAsync
        // directly: Aspire's AddNpgsqlDbContext turns connection retries on by default
        // (NpgsqlEntityFrameworkCorePostgreSQLSettings.DisableRetry defaults to false), and a
        // retrying strategy refuses a user-initiated transaction unless the whole transaction is
        // the retriable unit. Calling BeginTransactionAsync straight would pass every test here —
        // the test harness builds a plain UseNpgsql context with no retry strategy — and throw in
        // staging and production, which is the worst possible place to find out.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        var user = await strategy.ExecuteAsync(async () =>
        {
            // A retried attempt must start from nothing. EF does not revert entity states when a
            // transaction rolls back, so without this the replay would find the user already
            // tracked as Unchanged and the assignment rows already "saved" — re-inserting the
            // account and silently dropping its locations. Everything the attempt needs is
            // therefore read and built inside this delegate. Safe to clear here: by the time a
            // POST reaches this service everything else in the request (validation, the scope
            // check) has only read.
            dbContext.ChangeTracker.Clear();

            var primaryOrg = await dbContext.OrganisationNodes.FirstAsync(o => o.Id == orgNodeIds[0], cancellationToken);

            var invitee = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                FullName = fullName,
                OrgNodeId = primaryOrg.Id,
                HierarchyPath = primaryOrg.HierarchyPath,
                OrgLevel = primaryOrg.Level,
            };

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // No password — the account stays in the "Invited" state (PasswordHash is null) until
            // the user completes the set-password link.
            var createResult = await userManager.CreateAsync(invitee);
            if (!createResult.Succeeded)
            {
                throw new DomainRuleViolationException(Describe("Couldn't create the account", createResult));
            }

            // UserManager reports a refusal as an IdentityResult instead of throwing, so an
            // unchecked result is a failed step the transaction would happily commit over — which
            // is exactly how an invited user used to end up with no role at all.
            var roleResult = await userManager.AddToRoleAsync(invitee, role);
            if (!roleResult.Succeeded)
            {
                throw new DomainRuleViolationException(Describe($"Couldn't give the account the {role} role", roleResult));
            }

            foreach (var orgNodeId in orgNodeIds)
            {
                dbContext.UserOrgAssignments.Add(new UserOrgAssignment
                {
                    Id = Guid.NewGuid(),
                    UserId = invitee.Id,
                    OrgNodeId = orgNodeId,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return invitee;
        });

        // Deliberately after the commit. UserDirectoryController sends the invitation email off
        // the back of this return value, so a token minted inside the transaction would be a live
        // set-password link for an account a rollback then removed — the admin told nothing
        // happened while the invitee holds a working link. Throwing above returns no result at
        // all, so no link and no email. Nothing is lost by waiting: the token is a data-protected
        // payload over the user's security stamp, not a database write.
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
            throw new DomainRuleViolationException("Can't un-assign a user's primary org — switch their primary org first.");
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

    /// <summary>Identity's own error descriptions are already English sentences ("Username 'x' is
    /// already taken."), but on their own they don't say which step of the invite refused —
    /// prefixing them keeps the copy usable when it lands verbatim in the screen's validation
    /// summary (see DomainRuleViolationFilter).</summary>
    private static string Describe(string what, IdentityResult result) =>
        $"{what}: {string.Join("; ", result.Errors.Select(e => e.Description))}";

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

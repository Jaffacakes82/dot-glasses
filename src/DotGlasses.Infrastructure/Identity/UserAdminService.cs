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
    /// <summary>Shown in place of the name of an org the caller can't see: the assignment exists
    /// (and is why the caller may not suspend the user), but where it is isn't theirs to know.</summary>
    private const string OutsideScopeOrgName = "Outside your scope";

    public async Task<IReadOnlyList<UserAdminRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        var scopePaths = currentUser.ScopePaths;

        // The same LIKE ANY shape the global hierarchy filter uses (ADR-0004), over the raw column.
        // Hierarchy paths are digits and slashes only, so nothing needs escaping. No scope paths,
        // no patterns, no users.
        var patterns = scopePaths.Select(p => p.Value + "%").ToArray();

        // Read unscoped on purpose, with the scope applied by hand: a user's assignments outside
        // the caller's scope must still be seen, because they are what stops the caller acting on
        // the user as a whole. Deactivated orgs count too — the assignment still belongs to the
        // user and comes back into force if the org is reactivated.
        var inScopeOrgIds = dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(o => patterns.Any(p => EF.Functions.Like(o.HierarchyPath, p)))
            .Select(o => o.Id);

        var users = await userManager.Users
            .Where(u => dbContext.UserOrgAssignments.Any(a => a.UserId == u.Id && inScopeOrgIds.Contains(a.OrgNodeId)) ||
                // Transitional, until ticket 08 removes the column: the old active org counts as
                // one of the user's assignments, exactly as UserAccessLoader counts it.
                (u.OrgNodeId != null && inScopeOrgIds.Contains(u.OrgNodeId.Value)))
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

        var orgIds = assignments.Select(a => a.OrgNodeId)
            .Concat(users.Where(u => u.OrgNodeId.HasValue).Select(u => u.OrgNodeId!.Value))
            .Distinct()
            .ToList();
        var orgs = await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => (o.Name, Path: HierarchyPath.Parse(o.HierarchyPath)), cancellationToken);

        var rows = new List<UserAdminRow>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var assignedOrgIds = assignments.Where(a => a.UserId == user.Id).Select(a => a.OrgNodeId).ToList();
            var assignedOrgNames = assignedOrgIds
                .Select(id => orgs.TryGetValue(id, out var org) && InScope(org.Path) ? org.Name : OutsideScopeOrgName)
                .ToList();
            var assignmentPaths = assignedOrgIds
                .Concat(user.OrgNodeId is { } activeOrgId ? [activeOrgId] : []) // transitional — see the listing query above
                .Where(orgs.ContainsKey)
                .Select(id => orgs[id].Path)
                .Distinct()
                .ToList();

            rows.Add(new UserAdminRow(
                user.Id,
                user.Email ?? user.UserName ?? "—",
                user.DisplayName(),
                roles.FirstOrDefault() ?? "—",
                assignedOrgNames,
                assignedOrgIds,
                assignmentPaths,
                ResolveStatus(user),
                user.LastLoginUtc,
                salesCounts.GetValueOrDefault(user.Id, 0)));
        }

        return rows;

        bool InScope(HierarchyPath path) => scopePaths.Any(path.IsSelfOrDescendantOf);
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
        // directly: Aspire's AddAzureNpgsqlDbContext turns connection retries on by default
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

            // Transitional, until ticket 08 removes the columns: the account still needs an old
            // "active org" for the Field App's token. It is picked by level, most specific first,
            // so the order the orgs were ticked in means nothing — no assignment is special.
            var activeOrg = TransitionalActiveOrg(
                await dbContext.OrganisationNodes.Where(o => orgNodeIds.Contains(o.Id)).ToListAsync(cancellationToken));

            var invitee = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                FullName = fullName,
                OrgNodeId = activeOrg.Id,
                HierarchyPath = activeOrg.HierarchyPath,
                OrgLevel = activeOrg.Level,
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
        await userManager.SetLockoutEndDateAsync(user, UserSuspension.LockoutEnd);
    }

    public async Task UnsuspendAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        await userManager.SetLockoutEndDateAsync(user, null);
    }

    /// <summary>Two Identity writes — drop the old role, add the new one — so one transaction,
    /// opened through the execution strategy for the same reasons as InviteAsync's. Unchecked, a
    /// refused AddToRoleAsync would commit over the removal and leave the user with no role.</summary>
    public async Task ChangeRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!RoleNames.All.Contains(role))
        {
            throw new DomainRuleViolationException("Role must be one of: " + string.Join(", ", RoleNames.All) + ".");
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // A replayed attempt starts from nothing — EF doesn't revert entity states on
            // rollback. Safe here: everything earlier in the request (the scope check) only read.
            dbContext.ChangeTracker.Clear();

            var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
            var currentRoles = await userManager.GetRolesAsync(user);
            if (currentRoles.SequenceEqual([role]))
            {
                return;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                throw new DomainRuleViolationException(Describe("Couldn't remove the account's current role", removeResult));
            }

            var addResult = await userManager.AddToRoleAsync(user, role);
            if (!addResult.Succeeded)
            {
                throw new DomainRuleViolationException(Describe($"Couldn't give the account the {role} role", addResult));
            }

            await transaction.CommitAsync(cancellationToken);
        });
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
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");
        var assignments = await dbContext.UserOrgAssignments
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        var removed = assignments.FirstOrDefault(a => a.OrgNodeId == orgNodeId);
        if (removed is null)
        {
            return;
        }

        var remainingOrgIds = assignments.Where(a => a != removed).Select(a => a.OrgNodeId).ToList();
        if (remainingOrgIds.Count == 0)
        {
            throw new DomainRuleViolationException(
                "This is the user's last org assignment, so it can't be removed. To take away all of their access, suspend them instead.");
        }

        dbContext.UserOrgAssignments.Remove(removed);

        // Transitional, until ticket 08 removes the columns: UserAccessLoader still counts the old
        // active org as an assignment, so removing the assignment it points at would leave that
        // org's scope standing. Move it onto one the user keeps. Plain EF rather than UserManager,
        // so the removal and the move are one SaveChanges — one transaction, never half-applied.
        if (user.OrgNodeId == orgNodeId)
        {
            var next = TransitionalActiveOrg(await dbContext.OrganisationNodes
                .IgnoreQueryFilters()
                .Where(o => remainingOrgIds.Contains(o.Id))
                .ToListAsync(cancellationToken));

            user.OrgNodeId = next.Id;
            user.HierarchyPath = next.HierarchyPath;
            user.OrgLevel = next.Level;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Transitional, until ticket 08 removes ApplicationUser's active-org columns: which
    /// of a user's orgs fills them. The most specific one — so a technician's retail point wins
    /// over their country for the Field App's token — then by path, so the choice never depends on
    /// the order orgs were ticked or assigned in.</summary>
    private static OrganisationNode TransitionalActiveOrg(IEnumerable<OrganisationNode> orgs) =>
        orgs.OrderByDescending(o => o.Level).ThenBy(o => o.HierarchyPath, StringComparer.Ordinal).First();

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

        // Only a real suspension — not the temporary lockout anyone can trigger with wrong passwords.
        if (UserSuspension.IsSuspended(user.LockoutEnd))
        {
            return "Suspended";
        }

        return "Active";
    }
}

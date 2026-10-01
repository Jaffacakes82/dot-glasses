using DotGlasses.Application.Common;
using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotGlasses.Infrastructure.Identity;

public class UserAdminService(
    UserManager<ApplicationUser> userManager,
    DotGlassesDbContext dbContext,
    ILogger<UserAdminService> logger,
    ICurrentUserContext currentUser) : IUserAdminService
{
    private const string LastAssignmentMessage =
        "This is the user's last org assignment, so it can't be removed. To take away all of their access, suspend them instead.";

    private const string OwnRoleMessage = "You can't change your own role. Ask another admin to change it.";
    private const string OwnSuspensionMessage = "You can't suspend yourself. Ask another admin.";

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

        // A user with no assignment at all sits under nobody's org, so no scope path can claim
        // them — but someone has to be able to see them, or the state can't be repaired from the
        // portal (they have no access until assigned somewhere). They are listed for DGI-level
        // callers, whose scope is the whole tree, and for nobody else. The application never
        // leaves a user in that state (removing the last assignment is refused); it arises from
        // outside it, e.g. a database clear-down.
        var listsUnassigned = currentUser.HighestLevel == OrganisationLevel.Dgi;

        var users = await userManager.Users
            .Where(u => dbContext.UserOrgAssignments.Any(a => a.UserId == u.Id && inScopeOrgIds.Contains(a.OrgNodeId))
                || (listsUnassigned && !dbContext.UserOrgAssignments.Any(a => a.UserId == u.Id)))
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

        var orgIds = assignments.Select(a => a.OrgNodeId).Distinct().ToList();
        var orgs = await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(
                o => o.Id,
                // An assignment to a deactivated organisation is still the user's, and says so.
                o => (Name: o.IsDeleted ? o.Name + " (deactivated)" : o.Name, Path: HierarchyPath.Parse(o.HierarchyPath)),
                cancellationToken);

        var rows = new List<UserAdminRow>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var assignedOrgIds = assignments.Where(a => a.UserId == user.Id).Select(a => a.OrgNodeId).ToList();
            var assignedOrgNames = assignedOrgIds
                .Select(id => orgs.TryGetValue(id, out var org) && InScope(org.Path) ? org.Name : OutsideScopeOrgName)
                .ToList();
            var assignmentPaths = assignedOrgIds
                .Where(orgs.ContainsKey)
                .Select(id => orgs[id].Path)
                .Distinct()
                .ToList();

            rows.Add(new UserAdminRow(
                user.Id,
                user.Email ?? user.UserName ?? "—",
                user.DisplayName(),
                RoleNames.Primary(roles) ?? "—",
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
        // InviteUserRequestValidator already requires one; this keeps the invariant — a user
        // always has at least one assignment — true for any other caller too.
        if (orgNodeIds.Count == 0)
        {
            throw new DomainRuleViolationException("Choose at least one organisation.");
        }

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

            var invitee = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = false,
                FullName = fullName,
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
                dbContext.UserOrgAssignments.Add(NewAssignment(invitee.Id, orgNodeId));
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

    /// <summary>Two Identity writes — enable lockout, then set its end date — so one transaction,
    /// opened through the execution strategy for the same reasons as InviteAsync's. Unchecked, a
    /// refused SetLockoutEndDateAsync would commit over an enabled-but-not-actually-suspended
    /// account, which UserSuspension.IsSuspended reads by the end date alone.</summary>
    public async Task SuspendAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId == userId)
        {
            throw new DomainRuleViolationException(OwnSuspensionMessage);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // A replayed attempt starts from nothing — EF doesn't revert entity states on
            // rollback. Safe here: everything earlier in the request (the scope check) only read.
            dbContext.ChangeTracker.Clear();

            var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var enableResult = await userManager.SetLockoutEnabledAsync(user, true);
            if (!enableResult.Succeeded)
            {
                throw new DomainRuleViolationException(Describe("Couldn't suspend the account", enableResult));
            }

            var endDateResult = await userManager.SetLockoutEndDateAsync(user, UserSuspension.LockoutEnd);
            if (!endDateResult.Succeeded)
            {
                throw new DomainRuleViolationException(Describe("Couldn't suspend the account", endDateResult));
            }

            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <summary>Single Identity write, but still routed through the same transactional shape as
    /// SuspendAsync/ChangeRoleAsync — consistency of the pattern over saving a line, and it keeps
    /// this service's execution-strategy usage uniform rather than one write path being the odd
    /// one out.</summary>
    public async Task UnsuspendAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var result = await userManager.SetLockoutEndDateAsync(user, null);
            if (!result.Succeeded)
            {
                throw new DomainRuleViolationException(Describe("Couldn't unsuspend the account", result));
            }

            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <summary>Two Identity writes — drop the old role, add the new one — so one transaction,
    /// opened through the execution strategy for the same reasons as InviteAsync's. Unchecked, a
    /// refused AddToRoleAsync would commit over the removal and leave the user with no role.</summary>
    public async Task ChangeRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!RoleNames.All.Contains(role))
        {
            throw new DomainRuleViolationException("Choose a role.");
        }

        await UpdateAsync(userId, new UserEditPlan(NewFullName: null, NewRole: role, OrgsToAdd: [], OrgsToRemove: []), cancellationToken);
    }

    public async Task AssignUsersToOrgAsync(IReadOnlyList<Guid> userIds, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new DomainRuleViolationException("Tick at least one person to assign.");
        }

        // Checked against the caller's own directory before anything is written: someone the
        // caller can't see is assigned by someone higher up, and an Invited or Suspended user's
        // organisations are changed from their Edit page.
        var visible = (await ListAsync(cancellationToken)).ToDictionary(u => u.Id);
        foreach (var id in ids)
        {
            if (!visible.TryGetValue(id, out var row))
            {
                throw new DomainRuleViolationException(
                    "One of the people chosen isn't in the organisations you manage, so nobody was assigned. Ask someone higher up to assign them.");
            }

            if (row.Status != UserStatuses.Active)
            {
                throw new DomainRuleViolationException(
                    $"{row.DisplayName} is {row.Status.ToLowerInvariant()}, so nobody was assigned. Change their organisations from their Edit page.");
            }
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        var assigned = await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var org = await dbContext.OrganisationNodes.FirstAsync(o => o.Id == orgNodeId, cancellationToken);
            var already = await dbContext.UserOrgAssignments
                .Where(a => a.OrgNodeId == orgNodeId && ids.Contains(a.UserId))
                .Select(a => a.UserId)
                .ToListAsync(cancellationToken);

            var toAssign = ids.Except(already).ToList();
            foreach (var userId in toAssign)
            {
                dbContext.UserOrgAssignments.Add(NewAssignment(userId, orgNodeId));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (Users: toAssign, Org: org.Name);
        });

        foreach (var userId in assigned.Users)
        {
            LogChange(userId, $"assigned to {assigned.Org} ({orgNodeId})");
        }
    }

    public Task UnassignUserFromOrgAsync(Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default) =>
        UpdateAsync(userId, new UserEditPlan(NewFullName: null, NewRole: null, OrgsToAdd: [], OrgsToRemove: [orgNodeId]), cancellationToken);

    public async Task<UserEditDetail?> GetForEditAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // Unscoped on purpose, as in ListAsync: out-of-scope and deactivated organisations both
        // still count as this user's assignments.
        var orgs = await (
            from assignment in dbContext.UserOrgAssignments
            join org in dbContext.OrganisationNodes.IgnoreQueryFilters() on assignment.OrgNodeId equals org.Id
            where assignment.UserId == userId
            orderby org.HierarchyPath
            select org).ToListAsync(cancellationToken);

        var assignments = orgs
            .Select(o => new UserEditAssignment(o.Id, o.Name, o.Level, HierarchyPath.Parse(o.HierarchyPath), o.IsDeleted))
            .ToList();
        var inScope = assignments.Where(a => currentUser.ScopePaths.Any(a.Path.IsSelfOrDescendantOf)).ToList();

        // Seeing a user needs one assignment in scope; one with none at all is shown to DGI-level
        // callers only, so they can be repaired (see ListAsync).
        var visible = inScope.Count > 0 || (assignments.Count == 0 && currentUser.HighestLevel == OrganisationLevel.Dgi);
        if (!visible)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);

        return new UserEditDetail(
            user.Id,
            user.Email ?? user.UserName ?? "—",
            user.FullName ?? string.Empty,
            RoleNames.Primary(roles) ?? RoleNames.User,
            ResolveStatus(user),
            inScope,
            assignments.Count - inScope.Count,
            assignments.Select(a => a.Path).Distinct().ToList(),
            IsSelf: currentUser.UserId == userId);
    }

    /// <summary>Name, role and assignment changes are one unit of work, opened through the
    /// execution strategy for the same reasons as InviteAsync's, with every IdentityResult
    /// checked. Nothing is emailed to the user, and editing never unsuspends them.</summary>
    public async Task UpdateAsync(Guid userId, UserEditPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.NewRole is { } requestedRole && !RoleNames.All.Contains(requestedRole))
        {
            throw new DomainRuleViolationException("Choose a role.");
        }

        if (plan.NewFullName is { } requestedName && (requestedName.Length == 0 || requestedName.Length > 200))
        {
            throw new DomainRuleViolationException(requestedName.Length == 0
                ? "Enter the person's full name."
                : "Keep the full name to 200 characters or fewer.");
        }

        var isSelf = currentUser.UserId == userId;
        var strategy = dbContext.Database.CreateExecutionStrategy();

        var changes = await strategy.ExecuteAsync(async () =>
        {
            // A replayed attempt starts from nothing — EF doesn't revert entity states on
            // rollback. Safe here: everything earlier in the request (the scope checks) only read.
            dbContext.ChangeTracker.Clear();
            var made = new List<string>();

            var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            if (plan.NewFullName is { } name && name != user.FullName)
            {
                user.FullName = name;
                var nameResult = await userManager.UpdateAsync(user);
                if (!nameResult.Succeeded)
                {
                    throw new DomainRuleViolationException(Describe("Couldn't change the name", nameResult));
                }

                made.Add("name changed");
            }

            if (plan.NewRole is { } role)
            {
                var currentRoles = await userManager.GetRolesAsync(user);
                if (!currentRoles.SequenceEqual([role]))
                {
                    if (isSelf)
                    {
                        throw new DomainRuleViolationException(OwnRoleMessage);
                    }

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

                    made.Add($"role changed from {RoleNames.Primary(currentRoles) ?? "none"} to {role}");
                }
            }

            if (plan.OrgsToAdd.Count > 0 || plan.OrgsToRemove.Count > 0)
            {
                made.AddRange(await ApplyAssignmentChangesAsync(userId, plan, isSelf, cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return made;
        });

        foreach (var change in changes)
        {
            LogChange(userId, change);
        }
    }

    private async Task<List<string>> ApplyAssignmentChangesAsync(Guid userId, UserEditPlan plan, bool isSelf, CancellationToken cancellationToken)
    {
        var made = new List<string>();

        var assignments = await dbContext.UserOrgAssignments
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        // Past the filter: a deactivated organisation still holds assignments (they come back
        // into force when it is reactivated) and can still have one removed.
        var orgIds = assignments.Select(a => a.OrgNodeId).Concat(plan.OrgsToAdd).Distinct().ToList();
        var orgs = await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        var removed = new List<UserOrgAssignment>();
        foreach (var orgNodeId in plan.OrgsToRemove)
        {
            if (assignments.FirstOrDefault(a => a.OrgNodeId == orgNodeId) is { } assignment)
            {
                assignments.Remove(assignment);
                removed.Add(assignment);
                dbContext.UserOrgAssignments.Remove(assignment);
                made.Add($"unassigned from {orgs[orgNodeId].Name} ({orgNodeId})");
            }
        }

        foreach (var orgNodeId in plan.OrgsToAdd)
        {
            if (assignments.Any(a => a.OrgNodeId == orgNodeId))
            {
                continue;
            }

            // An id that names no organisation is a tampered form or a bug — a 500 (ADR-0003).
            var org = orgs[orgNodeId];
            if (org.IsDeleted)
            {
                throw new DomainRuleViolationException($"{org.Name} is deactivated, so nobody can be assigned to it.");
            }

            var added = NewAssignment(userId, orgNodeId);
            assignments.Add(added);
            dbContext.UserOrgAssignments.Add(added);
            made.Add($"assigned to {org.Name} ({orgNodeId})");
        }

        if (assignments.Count == 0)
        {
            throw new DomainRuleViolationException(LastAssignmentMessage);
        }

        if (isSelf)
        {
            var kept = assignments.Select(a => HierarchyPath.Parse(orgs[a.OrgNodeId].HierarchyPath)).ToList();
            foreach (var assignment in removed)
            {
                var org = orgs[assignment.OrgNodeId];
                if (OwnAssignments.RemovalShrinksScope(HierarchyPath.Parse(org.HierarchyPath), kept))
                {
                    throw new DomainRuleViolationException(
                        $"Removing {org.Name} would take away access you couldn't give back to yourself. Ask another admin to remove it.");
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return made;
    }

    private static UserOrgAssignment NewAssignment(Guid userId, Guid orgNodeId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        OrgNodeId = orgNodeId,
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    /// <summary>One structured entry per change to a user's access. There is no history screen
    /// (docs/open-issues.md); this is the trace.</summary>
    private void LogChange(Guid targetUserId, string change) =>
        logger.LogInformation(
            "User access change by {ActingUserId} on {TargetUserId}: {Change}",
            currentUser.UserId, targetUserId, change);

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
            return UserStatuses.Invited;
        }

        // Only a real suspension — not the temporary lockout anyone can trigger with wrong passwords.
        if (UserSuspension.IsSuspended(user.LockoutEnd))
        {
            return UserStatuses.Suspended;
        }

        return UserStatuses.Active;
    }
}

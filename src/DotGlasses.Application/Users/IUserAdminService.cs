using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Common;

namespace DotGlasses.Application.Users;

/// <summary>Admin-only user management — backs the Admin Portal's User Directory screen. Trades
/// in its own DTOs rather than DotGlasses.Infrastructure.Identity.ApplicationUser, since
/// Application must not reference Infrastructure; UserAdminService (Infrastructure) is where
/// UserManager/SignInManager get used freely.
///
/// Listing needs manual hierarchy filtering, unlike every other admin service so far —
/// ApplicationUser is an Identity/Infrastructure type, not a Domain entity implementing
/// IHierarchyScoped, so it was never in scope for DotGlassesDbContext's automatic global query
/// filter.</summary>
public interface IUserAdminService
{
    /// <summary>Every user with at least one org assignment in the caller's scope (any of
    /// ICurrentUserContext.ScopePaths) — seeing a user needs only one (ADR-0006). Acting on the
    /// user as a whole needs all of them; see UserAdminRow.AssignmentPaths.</summary>
    Task<IReadOnlyList<UserAdminRow>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Search/role/status-filtered, paged view for the User Directory screen itself —
    /// deliberately separate from ListAsync, which several other callers (Organisations'
    /// "Assign users", the resource-based CanManage checks) need as the full unfiltered scoped
    /// set. search matches DisplayName or Email (case-insensitive); role/status are exact
    /// matches. Filters before paging.</summary>
    Task<PagedResult<UserAdminRow>> ListPagedAsync(string? search, string? role, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Creates the user with no password (shows as "Invited" until they complete
    /// set-password), gives them role and one org assignment per orgNodeId — in any order, none
    /// of them special — as one unit of work, and returns a real Identity password-reset token for
    /// the set-password link.</summary>
    Task<InviteUserResult> InviteAsync(string email, string fullName, string role, IReadOnlyList<Guid> orgNodeIds, CancellationToken cancellationToken = default);

    /// <summary>Same token mechanism as InviteAsync — for an existing user's "Reset password."</summary>
    Task<string> RegeneratePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Identity's own lockout mechanism (a never-ending LockoutEnd — see
    /// UserSuspension), not a parallel IsActive flag.</summary>
    Task SuspendAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UnsuspendAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the user's one role with role, as one unit of work. Throws
    /// DomainRuleViolationException for a role that isn't one of RoleNames.All.</summary>
    Task ChangeRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);

    /// <summary>Adds orgNodeId as an org assignment for an existing user — the Organisations
    /// screen's "Assign users" action. No-op if already assigned.</summary>
    Task AssignUserToOrgAsync(Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default);

    /// <summary>Removes one org assignment — the Organisations screen's un-assign action. No-op if
    /// the pairing doesn't exist. Any assignment can go except the user's last one, which throws
    /// DomainRuleViolationException: a user always keeps at least one, and suspending them is how
    /// all their access is removed (CONTEXT.md, Org assignment).</summary>
    Task UnassignUserFromOrgAsync(Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default);
}

/// <summary>Status is "Invited" (no password set yet), "Suspended" (see UserSuspension — not a
/// temporary failed-sign-in lockout), or "Active" — derived from Identity's own fields, not a
/// stored column. OrgNodeIds is parallel to OrgNames (same order), so callers acting on a specific
/// assignment (e.g. un-assign) don't have to match by name; an assignment outside the caller's
/// scope is named "Outside your scope" rather than disclosed.
///
/// AssignmentPaths is every org the user is assigned to, inside the caller's scope or not — what
/// the "act on this user" rule checks: suspend, reset password and change role need all of them
/// within the caller's scope (ADR-0006).</summary>
public record UserAdminRow(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    IReadOnlyList<string> OrgNames,
    IReadOnlyList<Guid> OrgNodeIds,
    IReadOnlyList<HierarchyPath> AssignmentPaths,
    string Status,
    DateTimeOffset? LastLoginUtc,
    int SalesCount);

public record InviteUserResult(Guid UserId, string Email, string PasswordResetToken);

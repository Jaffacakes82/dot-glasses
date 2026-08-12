namespace DotGlasses.Application.Users;

/// <summary>Admin-only user management — backs the Admin Portal's User Directory screen. Trades
/// in its own DTOs rather than DotGlasses.Infrastructure.Identity.ApplicationUser, since
/// Application must not reference Infrastructure; UserAdminService (Infrastructure) is where
/// UserManager/SignInManager get used freely.
///
/// Listing needs manual hierarchy-prefix filtering, unlike every other admin service so far —
/// ApplicationUser is an Identity/Infrastructure type, not a Domain entity implementing
/// IHierarchyScoped, so it was never in scope for DotGlassesDbContext's automatic global query
/// filter.</summary>
public interface IUserAdminService
{
    Task<IReadOnlyList<UserAdminRow>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Creates the user with no password (shows as "Invited" until they complete
    /// set-password), assigns role and every orgNodeId as a UserOrgAssignment row, stamps the
    /// first orgNodeId as the primary org (denormalized onto OrgNodeId/HierarchyPath/OrgLevel —
    /// there's no "switch active org" UI yet to make a more elaborate primary-selection UX
    /// meaningful), and returns a real Identity password-reset token for the set-password
    /// link.</summary>
    Task<InviteUserResult> InviteAsync(string email, string fullName, string role, IReadOnlyList<Guid> orgNodeIds, CancellationToken cancellationToken = default);

    /// <summary>Same token mechanism as InviteAsync — for an existing user's "Reset password."</summary>
    Task<string> RegeneratePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Identity's own lockout mechanism (LockoutEnd = DateTimeOffset.MaxValue), not a
    /// parallel IsActive flag.</summary>
    Task SuspendAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UnsuspendAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Adds orgNodeId as an additional UserOrgAssignment for an existing user — the
    /// Organisations screen's "Assign users" action (2026-08-05). No-op if already assigned.
    /// Never touches the user's primary org (OrgNodeId/HierarchyPath/OrgLevel) — there's still no
    /// "switch active location" UI to make changing which org drives a multi-org user's JWT/
    /// cookie claims meaningful (see CLAUDE.md's [OPEN] items).</summary>
    Task AssignUserToOrgAsync(Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default);

    /// <summary>Removes a UserOrgAssignment row — the Organisations screen's un-assign action
    /// (Phase 6). No-op if the pairing doesn't exist. Throws if orgNodeId is the user's own
    /// primary org (ApplicationUser.OrgNodeId) — removing that would leave the user with no org
    /// driving their JWT/hierarchy scope, and there's no "switch primary" UI yet to move it
    /// first.</summary>
    Task UnassignUserFromOrgAsync(Guid userId, Guid orgNodeId, CancellationToken cancellationToken = default);
}

/// <summary>Status is "Invited" (no password set yet), "Suspended" (locked out), or "Active" —
/// derived from Identity's own fields, not a stored column. OrgNodeIds is parallel to OrgNames
/// (same order) — added alongside OrgNames (Phase 6) so callers needing to act on a specific
/// assignment (e.g. un-assign) don't have to match by name, which is only unique by
/// coincidence.</summary>
public record UserAdminRow(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    IReadOnlyList<string> OrgNames,
    IReadOnlyList<Guid> OrgNodeIds,
    Guid? PrimaryOrgNodeId,
    string Status,
    DateTimeOffset? LastLoginUtc,
    int SalesCount,
    string HierarchyPath);

public record InviteUserResult(Guid UserId, string Email, string PasswordResetToken);

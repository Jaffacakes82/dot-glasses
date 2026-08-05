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
}

/// <summary>Status is "Invited" (no password set yet), "Suspended" (locked out), or "Active" —
/// derived from Identity's own fields, not a stored column.</summary>
public record UserAdminRow(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    IReadOnlyList<string> OrgNames,
    string Status,
    DateTimeOffset? LastLoginUtc,
    int SalesCount,
    string HierarchyPath);

public record InviteUserResult(Guid UserId, string Email, string PasswordResetToken);

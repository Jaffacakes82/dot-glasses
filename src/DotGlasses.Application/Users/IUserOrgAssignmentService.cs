namespace DotGlasses.Application.Users;

/// <summary>Self-service org-switching for the currently authenticated user — distinct from
/// IUserAdminService, which is admin-driven management of *other* users. Backs the Field App's
/// Settings/"switch selling point" flow (see UserOrgAssignment's own doc comment: the assignable
/// set lives in UserOrgAssignment, the active selection lives on ApplicationUser.OrgNodeId/
/// HierarchyPath/OrgLevel). Resolves assigned org names via IUnscopedReportQueryService, not a
/// plain scoped query — a user's secondary assigned org can sit anywhere in the tree, not
/// necessarily inside their own *current* hierarchy scope, so a plain scoped query would silently
/// drop it (same class of bug already fixed twice for Dashboard/Event History's org-name
/// resolution — see CLAUDE.md).</summary>
public interface IUserOrgAssignmentService
{
    Task<IReadOnlyList<AssignedOrgSummary>> ListAssignedOrgsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Updates the user's active org (ApplicationUser.OrgNodeId/HierarchyPath/OrgLevel)
    /// to targetOrgNodeId — the caller must re-issue a fresh JWT/cookie afterwards for the new
    /// claims to take effect. Throws DomainRuleViolationException if targetOrgNodeId isn't one of
    /// the user's own UserOrgAssignment rows — never trust a client-submitted org Id without
    /// checking membership first.</summary>
    Task SwitchActiveOrgAsync(Guid userId, Guid targetOrgNodeId, CancellationToken cancellationToken = default);
}

public record AssignedOrgSummary(Guid OrgNodeId, string Name, bool IsActive);

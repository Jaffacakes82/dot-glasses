namespace DotGlasses.Application.Users;

/// <summary>Self-service org-switching for the currently authenticated user — distinct from
/// IUserAdminService, which is admin-driven management of *other* users. Backs the Field App's
/// Settings/"switch selling point" flow (see UserOrgAssignment's own doc comment: the assignable
/// set lives in UserOrgAssignment, the active selection lives on ApplicationUser.OrgNodeId/
/// HierarchyPath/OrgLevel) and, on the Admin Portal, choosing which of the caller's own
/// assignments owns a new lens set. Resolves assigned org names via IUnscopedReportQueryService,
/// not a plain scoped query — a user's secondary assigned org can sit anywhere in the tree, not
/// necessarily inside their own *current* hierarchy scope, so a plain scoped query would silently
/// drop it (same class of bug already fixed twice for Dashboard/Event History's org-name
/// resolution — see CLAUDE.md).</summary>
public interface IUserOrgAssignmentService
{
    Task<IReadOnlyList<AssignedOrgSummary>> ListAssignedOrgsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's own assignments at Dgi or Country level only, id+name resolved —
    /// exactly the set a new PresetCatalogue's owning org may be chosen from
    /// (PresetCatalogueAdminService.CreateAsync enforces the same Dgi/Country rule; ADR-0006's
    /// "lens set creation" screen note). Ordered Dgi first, then by name, matching the
    /// Organisations screen's own level-then-name ordering.</summary>
    Task<IReadOnlyList<OwningOrgOption>> ListDgiOrCountryAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Updates the user's active org (ApplicationUser.OrgNodeId/HierarchyPath/OrgLevel)
    /// to targetOrgNodeId — the caller must re-issue a fresh JWT/cookie afterwards for the new
    /// claims to take effect. Throws DomainRuleViolationException if targetOrgNodeId isn't one of
    /// the user's own UserOrgAssignment rows — never trust a client-submitted org Id without
    /// checking membership first.</summary>
    Task SwitchActiveOrgAsync(Guid userId, Guid targetOrgNodeId, CancellationToken cancellationToken = default);
}

public record AssignedOrgSummary(Guid OrgNodeId, string Name, bool IsActive);

/// <summary>A Dgi/Country-level org the user is assigned to — a candidate to own a new lens set.</summary>
public record OwningOrgOption(Guid OrgNodeId, string Name);

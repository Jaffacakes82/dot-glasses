namespace DotGlasses.Web.Authorization;

public static class AuthorizationPolicies
{
    /// <summary>Any role, Country level and above (2026-08-04 decision: restrict Custom Orders
    /// to DGI/Country, hidden entirely below that).</summary>
    public const string CustomOrdersView = "CustomOrders.View";

    /// <summary>Admin only, DGI level only — the only role/level that can edit reference data.</summary>
    public const string ReferenceDataManage = "ReferenceData.Manage";

    /// <summary>Admin, Country level and above (2026-08-04 decision: DGI/Country can
    /// create and assign preset catalogues).</summary>
    public const string PresetCatalogueManage = "PresetCatalogue.Manage";

    /// <summary>PresetCatalogue.Manage, plus resource-based against the lens set's <em>owning</em>
    /// org: the caller must be at or above it. Wired to every CataloguesController action that
    /// changes what a lens set is (edit, add/remove lens powers, retire/reactivate) — a lens set
    /// assigned across countries must not be changed by one country's admin (ADR-0005).</summary>
    public const string PresetCatalogueEditInScope = "PresetCatalogue.EditInScope";

    /// <summary>PresetCatalogue.Manage, plus resource-based against the org being assigned to (or
    /// unassigned from): it must be at or below the caller. Any active lens set may be assigned —
    /// only the caller's own part of the tree is affected.</summary>
    public const string PresetCatalogueAssignInScope = "PresetCatalogue.AssignInScope";

    /// <summary>Admin, resource-based against a whole user (UserAssignments): every one of the
    /// user's org assignments must be in the caller's scope. Wired to every UserDirectoryController
    /// action (Invite — against the orgs being assigned — ResetPassword, Suspend, Unsuspend,
    /// ChangeRole) — see AllAssignmentsInScopeRequirement.</summary>
    public const string ManageUsersInScope = "Users.ManageInScope";

    /// <summary>Admin, resource-based against the target org, which must be in the caller's
    /// scope. Wired to every OrganisationsController write action (CreateChild, the two flag
    /// toggles, AssignUser, UnassignUser) — adding or removing one of a user's assignments needs
    /// only that org in scope, not all of the user's.</summary>
    public const string ManageOrgInScope = "Organisations.ManageInScope";
}

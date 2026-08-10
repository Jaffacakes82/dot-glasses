namespace DotGlasses.Web.Authorization;

public static class AuthorizationPolicies
{
    public const string WidgetExampleCreate = "WidgetExample.Create";

    /// <summary>Any role, Country level and above (2026-08-04 decision: restrict Custom Orders
    /// to DGI/Country, hidden entirely below that).</summary>
    public const string CustomOrdersView = "CustomOrders.View";

    /// <summary>Admin only, DGI level only — the only role/level that can edit reference data.</summary>
    public const string ReferenceDataManage = "ReferenceData.Manage";

    /// <summary>Admin, Country level and above (2026-08-04 decision: DGI/Country can
    /// create and assign preset catalogues).</summary>
    public const string PresetCatalogueManage = "PresetCatalogue.Manage";

    /// <summary>Admin, resource-based against the target user's org. Wired to every
    /// UserDirectoryController action (Invite/ResetPassword/Suspend/Unsuspend) — see
    /// HierarchyDescendantRequirement.</summary>
    public const string ManageUsersInScope = "Users.ManageInScope";

    /// <summary>Admin, resource-based against the target org — same scope rule as
    /// ManageUsersInScope. Wired to every OrganisationsController write action (CreateChild, the
    /// two flag toggles, AssignUser).</summary>
    public const string ManageOrgInScope = "Organisations.ManageInScope";
}

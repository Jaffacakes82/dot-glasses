namespace DotGlasses.Web.Authorization;

public static class AuthorizationPolicies
{
    public const string WidgetExampleCreate = "WidgetExample.Create";

    /// <summary>Any role, Country level and above (2026-08-04 decision: restrict Custom Orders
    /// to DGI/Country, hidden entirely below that).</summary>
    public const string CustomOrdersView = "CustomOrders.View";

    /// <summary>Admin only, DGI level only — the only role/level that can edit reference data.</summary>
    public const string ReferenceDataManage = "ReferenceData.Manage";

    /// <summary>Admin/Manager, Country level and above (2026-08-04 decision: DGI/Country can
    /// create and assign preset catalogues).</summary>
    public const string PresetCatalogueManage = "PresetCatalogue.Manage";

    /// <summary>Admin/Manager, resource-based against the target user's org — a Manager can
    /// manage any role at/below their own node (2026-08-04 decision). Not yet wired to a
    /// controller — see HierarchyDescendantRequirement.</summary>
    public const string ManageUsersInScope = "Users.ManageInScope";

    /// <summary>Admin/Manager, resource-based against the target org — same scope rule as
    /// ManageUsersInScope. Not yet wired to a controller.</summary>
    public const string ManageOrgInScope = "Organisations.ManageInScope";
}

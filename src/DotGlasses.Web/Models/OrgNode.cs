namespace DotGlasses.Web.Models;

/// <summary>Real org hierarchy tree node — Type is OrganisationLevel's display string ("DGI",
/// "Country", "Intermediate", "RetailPoint"); Kind is the entity's free-text display label (e.g.
/// "Retailer", "Distributor"), shown separately since no business rule keys off it.</summary>
public record OrgNode(Guid Id, string Name, string Type, string? Kind, bool IsTrainingOrg, bool CanHandleCustomOrders, IReadOnlyList<OrgNode> Children)
{
    public static readonly IReadOnlyDictionary<string, string> TypeColor = new Dictionary<string, string>
    {
        ["DGI"] = "var(--dot-black)",
        ["Country"] = "var(--dot-blue)",
        ["Intermediate"] = "var(--dot-orange)",
        ["RetailPoint"] = "var(--dot-green)",
    };
}

/// <summary>Tree (left panel) + the currently selected node (right detail panel) — CanManage
/// drives whether "Add child node", the two flag toggles, and "Assign users" are shown for
/// Selected, per AuthorizationPolicies.ManageOrgInScope resolved against Selected's own
/// HierarchyPath (the same check for all four — see CLAUDE.md's Assign users section: this
/// reuses org-scoped ManageOrgInScope rather than the separate user-scoped
/// ManageUsersInScope, which stays reserved for User Directory's own future actions).
/// AssignableUsers is every user in the caller's own hierarchy scope (IUserAdminService.
/// ListAsync, already scoped); SelectedAssignedUserNames is who's already assigned to
/// Selected specifically.</summary>
public record OrganisationsIndexViewModel(
    OrgNode Tree,
    OrgNode Selected,
    bool CanManage,
    IReadOnlyList<(string Value, string Label)> ValidChildLevels,
    IReadOnlyList<(Guid Id, string DisplayName)> AssignableUsers,
    IReadOnlyList<string> SelectedAssignedUserNames);

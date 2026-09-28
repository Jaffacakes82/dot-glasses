namespace DotGlasses.Web.Models;

/// <summary>Real org hierarchy tree node — Type is OrganisationLevel's display string ("DGI",
/// "Country", "Intermediate", "RetailPoint"); Kind is the entity's free-text display label (e.g.
/// "Retailer", "Distributor"), shown separately since no business rule keys off it.</summary>
public record OrgNode(Guid Id, string Name, string Type, string? Kind, bool IsTrainingOrg, IReadOnlyList<OrgNode> Children)
{
    public static readonly IReadOnlyDictionary<string, string> TypeColor = new Dictionary<string, string>
    {
        ["DGI"] = "var(--dot-black)",
        ["Country"] = "var(--dot-blue)",
        ["Intermediate"] = "var(--dot-orange)",
        ["RetailPoint"] = "var(--dot-green)",
    };
}

/// <summary>Trees (left panel) + the currently selected node (right detail panel) — one tree per
/// separate part of the caller's scope (ADR-0006/CONTEXT.md "Scope"): each entry in Trees is a
/// node whose parent is either absent or outside the caller's scoped node set, ordered highest
/// level first then alphabetically (OrganisationsController.BuildTrees). A caller with a nested
/// assignment (e.g. DGI plus a retail point beneath it) sees exactly one tree, because the scope
/// paths behind ListAsync's query filter are already collapsed (HierarchyPath.Outermost) before
/// the query runs — the retail point's own row never surfaces as a second root. CanManage
/// drives whether the level-appropriate "Add ..." action, the two flag toggles, "Rename", "Deactivate" and
/// "Assign users" are shown for Selected, per AuthorizationPolicies.ManageOrgInScope resolved
/// against Selected's own HierarchyPath (the same check for all of them — see CLAUDE.md's Assign
/// users section: this reuses org-scoped ManageOrgInScope rather than the separate user-scoped
/// ManageUsersInScope, which stays reserved for User Directory's own future actions).
/// AssignableUsers is every user in the caller's own hierarchy scope (IUserAdminService.
/// ListAsync, already scoped); SelectedAssignedUsers is who's already assigned to Selected
/// specifically, matched by OrgNodeId (Phase 6 — previously matched by name, see CLAUDE.md).
/// DeactivatedNodes is the caller's own deactivated orgs, shown separately since a deactivated
/// node no longer appears in Trees at all (the standard scoped query filters it out).</summary>
public record OrganisationsIndexViewModel(
    IReadOnlyList<OrgNode> Trees,
    OrgNode Selected,
    bool CanManage,
    bool SelectedHasChildren,
    IReadOnlyList<(string Value, string Label)> ValidChildLevels,
    IReadOnlyList<(Guid Id, string DisplayName)> AssignableUsers,
    IReadOnlyList<(Guid UserId, string DisplayName)> SelectedAssignedUsers,
    IReadOnlyList<(Guid Id, string Name)> DeactivatedNodes);

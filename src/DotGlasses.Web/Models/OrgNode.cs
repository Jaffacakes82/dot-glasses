using DotGlasses.Application.Organisations;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

/// <summary>Real org hierarchy tree node. A view decides behaviour by <see cref="Level"/> and
/// shows <see cref="LevelLabel"/> — never the other way round.</summary>
public record OrgNode(Guid Id, string Name, OrganisationLevel Level, bool IsTrainingOrg, IReadOnlyList<OrgNode> Children)
{
    public string LevelLabel => OrganisationLevelLabels.For(Level);

    public string LevelColor => Level switch
    {
        OrganisationLevel.Dgi => "var(--dot-black)",
        OrganisationLevel.Country => "var(--dot-blue)",
        OrganisationLevel.Intermediate => "var(--dot-orange)",
        _ => "var(--dot-green)",
    };

    public bool IsRetailPoint => Level == OrganisationLevel.RetailPoint;
}

/// <summary>A user the Assign users dialog offers: Active, visible to the caller, and not already
/// assigned to the selected organisation.</summary>
public record AssignableUser(Guid Id, string DisplayName, string Email, string Role);

public record AssignedUser(Guid UserId, string DisplayName, string Role);

/// <summary>One entry in the Deactivated orgs strip: the top organisation of a group deactivated
/// together, and how many organisations beneath it went with it.</summary>
public record DeactivatedGroup(Guid Id, string Name, int BeneathCount);

/// <summary>What the Deactivate confirmation states for the selected organisation: how many
/// organisations beneath it go too, and how many people hold an assignment somewhere in it.</summary>
public record DeactivationPreview(int OrganisationsBeneath, int PeopleAssigned, string? BlockedReason = null);

/// <summary>Trees (left panel) + the currently selected node (right detail panel) — one tree per
/// separate part of the caller's scope (ADR-0006/CONTEXT.md "Scope"): each entry in Trees is a
/// node whose parent is either absent or outside the caller's scoped node set, ordered highest
/// level first then alphabetically (OrganisationsController.BuildTrees). A caller with a nested
/// assignment (e.g. DGI plus a retail point beneath it) sees exactly one tree, because the scope
/// paths behind ListAsync's query filter are already collapsed (HierarchyPath.Outermost) before
/// the query runs — the retail point's own row never surfaces as a second root. CanManage
/// drives whether the "Add ..." action, the training flag toggle, "Rename", "Deactivate" and
/// "Assign users" are shown for Selected, per AuthorizationPolicies.ManageOrgInScope resolved
/// against Selected's own HierarchyPath (the same check for all of them: this reuses org-scoped
/// ManageOrgInScope rather than the separate user-scoped ManageUsersInScope, which is the User
/// Directory's). AssignableUsers and SelectedAssignedUsers come from IUserAdminService.ListAsync,
/// already scoped to the caller. DeactivatedGroups is the caller's own deactivated orgs, shown
/// separately since a deactivated node no longer appears in Trees at all (the standard scoped
/// query filters it out).</summary>
public record OrganisationsIndexViewModel(
    IReadOnlyList<OrgNode> Trees,
    OrgNode Selected,
    bool CanManage,
    IReadOnlyList<(string Value, string Label)> ValidChildLevels,
    IReadOnlyList<AssignableUser> AssignableUsers,
    IReadOnlyList<AssignedUser> SelectedAssignedUsers,
    IReadOnlyList<DeactivatedGroup> DeactivatedGroups,
    DeactivationPreview Deactivation);

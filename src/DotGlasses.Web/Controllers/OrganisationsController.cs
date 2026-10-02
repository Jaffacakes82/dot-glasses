using DotGlasses.Application.Common;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Export;
using DotGlasses.Web.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class OrganisationsController(
    IOrganisationAdminService organisationAdminService,
    IUserAdminService userAdminService,
    IAuthorizationService authorizationService,
    IValidator<CreateChildOrganisationRequest> createChildValidator,
    IValidator<RenameOrganisationRequest> renameValidator,
    ICurrentUserContext currentUser) : Controller
{
    public Task<IActionResult> Index(Guid? selectedId, CancellationToken cancellationToken) =>
        IndexViewAsync(selectedId, cancellationToken);

    /// <summary>Drives off the same ListAsync used to build the tree — already hierarchy-scoped
    /// to the caller's own subtree, no separate query path.</summary>
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var nodes = await organisationAdminService.ListAsync(cancellationToken);
        var csv = CsvExport.Build(
            ["Name", "Level", "HierarchyPath", "IsTrainingOrg"],
            nodes.Select(n => (IReadOnlyList<string?>)[n.Name, OrganisationLevelLabels.For(n.Level), n.HierarchyPath, n.IsTrainingOrg.ToString()]));

        return File(csv, "text/csv", $"organisations-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateChild(CreateChildOrganisationRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(request.ParentId, cancellationToken))
        {
            return Forbid();
        }

        var validationResult = await createChildValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return await IndexViewAsync(request.ParentId, cancellationToken);
        }

        var created = await organisationAdminService.CreateChildAsync(request.ParentId, request.Name, request.Level, cancellationToken);
        return RedirectToAction(nameof(Index), new { selectedId = created.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTrainingOrgFlag(Guid id, bool value, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(id, cancellationToken))
        {
            return Forbid();
        }

        await organisationAdminService.SetTrainingOrgFlagAsync(id, value, cancellationToken);
        return RedirectToAction(nameof(Index), new { selectedId = id });
    }

    /// <summary>Assigns everyone ticked in the Assign users dialog to one organisation, all or
    /// nothing. Reuses ManageOrgInScope (against the org being assigned into), not the separate
    /// user-scoped ManageUsersInScope — see OrganisationsIndexViewModel's doc comment for why.
    /// Who may be assigned this way (Active users the caller can see) is the service's rule.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUsers(Guid orgNodeId, List<Guid> userIds, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(orgNodeId, cancellationToken))
        {
            return Forbid();
        }

        await userAdminService.AssignUsersToOrgAsync(userIds, orgNodeId, cancellationToken);
        return RedirectToAction(nameof(Index), new { selectedId = orgNodeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignUser(Guid orgNodeId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(orgNodeId, cancellationToken))
        {
            return Forbid();
        }

        await userAdminService.UnassignUserFromOrgAsync(userId, orgNodeId, cancellationToken);
        return RedirectToAction(nameof(Index), new { selectedId = orgNodeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(RenameOrganisationRequest request, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(request.Id, cancellationToken))
        {
            return Forbid();
        }

        var validationResult = await renameValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return await IndexViewAsync(request.Id, cancellationToken);
        }

        await organisationAdminService.RenameAsync(request.Id, request.Name, cancellationToken);
        return RedirectToAction(nameof(Index), new { selectedId = request.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid id, bool value, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(id, cancellationToken))
        {
            return Forbid();
        }

        await organisationAdminService.SetActiveAsync(id, value, cancellationToken);
        return value ? RedirectToAction(nameof(Index), new { selectedId = id }) : RedirectToAction(nameof(Index));
    }

    /// <summary>Resource-based check against the target node's own HierarchyPath — see
    /// HierarchyDescendantRequirement. Re-checked here even though the view already hides the
    /// triggering button/form for a user who'd fail it; never trust the hidden-button UX alone.
    /// Falls back to ListDeactivatedAsync if the target isn't in the active list — reactivating a
    /// deactivated node is the one action whose own target is, by definition, invisible to
    /// ListAsync.</summary>
    private async Task<bool> CanManageAsync(Guid targetNodeId, CancellationToken cancellationToken)
    {
        var nodes = await organisationAdminService.ListAsync(cancellationToken);
        var target = nodes.FirstOrDefault(n => n.Id == targetNodeId);
        if (target is null)
        {
            var deactivatedNodes = await organisationAdminService.ListDeactivatedAsync(cancellationToken);
            target = deactivatedNodes.FirstOrDefault(n => n.Id == targetNodeId);
        }

        if (target is null)
        {
            return false;
        }

        var result = await authorizationService.AuthorizeAsync(User, target.HierarchyPath, AuthorizationPolicies.ManageOrgInScope);
        return result.Succeeded;
    }

    /// <summary>The screen, or its empty state when the caller's scope holds no organisation at
    /// all — an account with no assignment (it shouldn't exist, but a database clear-down leaves
    /// one behind), or one whose only assignments are to deactivated orgs. The scope filter has
    /// already answered "nothing" by then; this is only the screen saying so instead of failing
    /// on a tree with no root to select.</summary>
    private async Task<IActionResult> IndexViewAsync(Guid? selectedId, CancellationToken cancellationToken) =>
        await BuildViewModelAsync(selectedId, cancellationToken) is { } model
            ? View(nameof(Index), model)
            : View("NoOrganisations");

    private async Task<OrganisationsIndexViewModel?> BuildViewModelAsync(Guid? selectedId, CancellationToken cancellationToken)
    {
        var nodes = await organisationAdminService.ListAsync(cancellationToken);
        if (nodes.Count == 0)
        {
            return null;
        }

        var byId = nodes.ToDictionary(n => n.Id);
        var byParent = nodes.ToLookup(n => n.ParentId);

        // One tree per separate part of the caller's scope (ADR-0006, spec user stories 5-6): a
        // root is any node with no visible parent — the true DGI root for a DGI Admin, or e.g.
        // Kenya for a Kenya-level Admin (Kenya's real ParentId points at DGI, but DGI is filtered
        // out of their scoped result, so it's effectively their root). A nested assignment (DGI
        // plus a retail point beneath it) surfaces only one root here because ScopePaths already
        // collapsed it before ListAsync's query ran — the retail point's own row is simply absent
        // from "no visible parent", not merely deduplicated after the fact.
        var trees = BuildTrees(byId, byParent);

        var selected = (selectedId.HasValue ? FindNode(trees, selectedId.Value) : null) ?? trees[0];
        var selectedAdmin = byId[selected.Id];

        var canManage = (await authorizationService.AuthorizeAsync(User, selectedAdmin.HierarchyPath, AuthorizationPolicies.ManageOrgInScope)).Succeeded;

        var validChildLevels = new[] { OrganisationLevel.Country, OrganisationLevel.Intermediate, OrganisationLevel.RetailPoint }
            .Where(level => organisationAdminService.IsValidChildLevel(selectedAdmin.Level, level))
            .Select(level => (Value: level.ToString(), Label: OrganisationLevelLabels.For(level)))
            .ToList();

        var users = await userAdminService.ListAsync(cancellationToken);

        // Only Active users not already here: an Invited or Suspended user is assigned from their
        // Edit page, where the admin sees the whole of what they are changing.
        var assignableUsers = users
            .Where(u => u.Status == UserStatuses.Active && !u.OrgNodeIds.Contains(selected.Id))
            .Select(u => new AssignableUser(u.Id, u.DisplayName, u.Email, u.Role))
            .ToList();
        var selectedAssignedUsers = users
            .Where(u => u.OrgNodeIds.Contains(selected.Id))
            .Select(u => new AssignedUser(u.Id, u.DisplayName, u.Role))
            .ToList();

        var subtreeIds = Flatten(selected).Select(n => n.Id).ToHashSet();
        var deactivation = new DeactivationPreview(
            OrganisationsBeneath: subtreeIds.Count - 1,
            PeopleAssigned: users.Count(u => u.OrgNodeIds.Any(subtreeIds.Contains)),
            BlockedReason: OwnAccess.ComesThrough(HierarchyPath.Parse(selectedAdmin.HierarchyPath), currentUser.ScopePaths)
                ? OwnAccess.DeactivationRefusal
                : null);

        return new OrganisationsIndexViewModel(
            trees, selected, canManage, validChildLevels, assignableUsers, selectedAssignedUsers,
            DeactivatedGroups(await organisationAdminService.ListDeactivatedAsync(cancellationToken)), deactivation);
    }

    private static IEnumerable<OrgNode> Flatten(OrgNode node) => node.Children.SelectMany(Flatten).Prepend(node);

    /// <summary>The Deactivated orgs strip: each group once, by its top organisation, with how
    /// many went with it. A top is a deactivated organisation whose parent isn't itself in the
    /// list — anything beneath a deactivated organisation can't be reactivated until that one is,
    /// so it isn't offered. An organisation deactivated on its own is a group of one.</summary>
    private static IReadOnlyList<DeactivatedGroup> DeactivatedGroups(IReadOnlyList<OrganisationAdminNode> deactivated)
    {
        var ids = deactivated.Select(n => n.Id).ToHashSet();

        return deactivated
            .Where(n => n.ParentId is null || !ids.Contains(n.ParentId.Value))
            .Select(top => new DeactivatedGroup(
                top.Id,
                top.Name,
                top.DeactivationGroupId is { } groupId
                    ? deactivated.Count(n => n.Id != top.Id && n.DeactivationGroupId == groupId && HierarchyPath.Parse(n.HierarchyPath).IsSelfOrDescendantOf(HierarchyPath.Parse(top.HierarchyPath)))
                    : 0))
            .ToList();
    }

    /// <summary>Every root in the scoped node set — highest level first (DGI, then Country, ...),
    /// alphabetical within a level, per the ticket's ordering rule.</summary>
    private static IReadOnlyList<OrgNode> BuildTrees(
        IReadOnlyDictionary<Guid, OrganisationAdminNode> byId, ILookup<Guid?, OrganisationAdminNode> byParent) =>
        byId.Values
            .Where(n => n.ParentId is null || !byId.ContainsKey(n.ParentId.Value))
            .OrderBy(n => n.Level)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .Select(n => BuildTree(n.Id, byId, byParent))
            .ToList();

    private static OrgNode BuildTree(Guid nodeId, IReadOnlyDictionary<Guid, OrganisationAdminNode> byId, ILookup<Guid?, OrganisationAdminNode> byParent)
    {
        var node = byId[nodeId];
        var children = byParent[nodeId]
            .OrderBy(c => c.Name)
            .Select(c => BuildTree(c.Id, byId, byParent))
            .ToList();

        return new OrgNode(node.Id, node.Name, node.Level, node.IsTrainingOrg, children);
    }

    private static OrgNode? FindNode(IReadOnlyList<OrgNode> trees, Guid id) =>
        trees.Select(t => FindNode(t, id)).FirstOrDefault(n => n is not null);

    private static OrgNode? FindNode(OrgNode node, Guid id) =>
        node.Id == id ? node : node.Children.Select(c => FindNode(c, id)).FirstOrDefault(n => n is not null);
}

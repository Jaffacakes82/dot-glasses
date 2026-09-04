using DotGlasses.Application.Organisations;
using DotGlasses.Application.Users;
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
    IValidator<RenameOrganisationRequest> renameValidator) : Controller
{
    public async Task<IActionResult> Index(Guid? selectedId, CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(selectedId, cancellationToken));

    /// <summary>Drives off the same ListAsync used to build the tree — already hierarchy-scoped
    /// to the caller's own subtree, no separate query path.</summary>
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var nodes = await organisationAdminService.ListAsync(cancellationToken);
        var csv = CsvExport.Build(
            ["Name", "Level", "Kind", "HierarchyPath", "IsTrainingOrg"],
            nodes.Select(n => (IReadOnlyList<string?>)[n.Name, n.Level.ToString(), n.Kind, n.HierarchyPath, n.IsTrainingOrg.ToString()]));

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
            return View(nameof(Index), await BuildViewModelAsync(request.ParentId, cancellationToken));
        }

        var created = await organisationAdminService.CreateChildAsync(request.ParentId, request.Name, request.Level, request.Kind, cancellationToken);
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

    /// <summary>Reuses ManageOrgInScope (against the org being assigned into), not the separate
    /// user-scoped ManageUsersInScope — see OrganisationsIndexViewModel's doc comment for why.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignUser(Guid orgNodeId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(orgNodeId, cancellationToken))
        {
            return Forbid();
        }

        await userAdminService.AssignUserToOrgAsync(userId, orgNodeId, cancellationToken);
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
            return View(nameof(Index), await BuildViewModelAsync(request.Id, cancellationToken));
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

    private async Task<OrganisationsIndexViewModel> BuildViewModelAsync(Guid? selectedId, CancellationToken cancellationToken)
    {
        var nodes = await organisationAdminService.ListAsync(cancellationToken);
        var byId = nodes.ToDictionary(n => n.Id);
        var byParent = nodes.ToLookup(n => n.ParentId);

        // Root for display purposes is whichever node has no visible parent — the true DGI root
        // for a DGI Admin, or e.g. Kenya for a Kenya-level Admin (Kenya's real ParentId points at
        // DGI, but DGI is filtered out of their scoped result, so it's effectively their root).
        var rootId = nodes.First(n => n.ParentId is null || !byId.ContainsKey(n.ParentId.Value)).Id;
        var tree = BuildTree(rootId, byId, byParent);

        var selected = (selectedId.HasValue ? FindNode(tree, selectedId.Value) : null) ?? tree;
        var selectedAdmin = byId[selected.Id];

        var canManage = (await authorizationService.AuthorizeAsync(User, selectedAdmin.HierarchyPath, AuthorizationPolicies.ManageOrgInScope)).Succeeded;

        var validChildLevels = new[] { OrganisationLevel.Country, OrganisationLevel.Intermediate, OrganisationLevel.RetailPoint }
            .Where(level => organisationAdminService.IsValidChildLevel(selectedAdmin.Level, level))
            .Select(level => (Value: level.ToString(), Label: ChildLevelLabel(level)))
            .ToList();

        var users = await userAdminService.ListAsync(cancellationToken);
        var assignableUsers = users.Select(u => (u.Id, DisplayName: $"{u.DisplayName} ({u.Email})")).ToList();
        var selectedAssignedUsers = users
            .Where(u => u.OrgNodeIds.Contains(selected.Id))
            .Select(u => (u.Id, u.DisplayName))
            .ToList();

        var deactivatedNodes = (await organisationAdminService.ListDeactivatedAsync(cancellationToken))
            .Select(n => (n.Id, n.Name))
            .ToList();

        return new OrganisationsIndexViewModel(tree, selected, canManage, selected.Children.Count > 0, validChildLevels, assignableUsers, selectedAssignedUsers, deactivatedNodes);
    }

    private static OrgNode BuildTree(Guid nodeId, IReadOnlyDictionary<Guid, OrganisationAdminNode> byId, ILookup<Guid?, OrganisationAdminNode> byParent)
    {
        var node = byId[nodeId];
        var children = byParent[nodeId]
            .OrderBy(c => c.Name)
            .Select(c => BuildTree(c.Id, byId, byParent))
            .ToList();

        return new OrgNode(node.Id, node.Name, LevelDisplay(node.Level), node.Kind, node.IsTrainingOrg, children);
    }

    private static OrgNode? FindNode(OrgNode node, Guid id) =>
        node.Id == id ? node : node.Children.Select(c => FindNode(c, id)).FirstOrDefault(n => n is not null);

    /// <summary>The tree/badge display for a node's own level ("DGI" for the root, otherwise the
    /// raw enum name) — contrast with <see cref="ChildLevelLabel"/> below, which labels the level
    /// a new child is about to be created at, not an existing node's own level.</summary>
    private static string LevelDisplay(OrganisationLevel level) => level == OrganisationLevel.Dgi ? "DGI" : level.ToString();

    /// <summary>Level-appropriate label for the "add child" action and its target-level dropdown
    /// — replaces the generic "node" wording the Organisations screen used to show regardless of
    /// what level/kind was actually being added.</summary>
    private static string ChildLevelLabel(OrganisationLevel level) => level switch
    {
        OrganisationLevel.Country => "Country office",
        OrganisationLevel.Intermediate => "Retailer/distributor",
        OrganisationLevel.RetailPoint => "Retail point",
        _ => level.ToString(),
    };
}

using DotGlasses.Application.Organisations;
using DotGlasses.Domain.Enums;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class OrganisationsController(
    IOrganisationAdminService organisationAdminService,
    IAuthorizationService authorizationService,
    IValidator<CreateChildOrganisationRequest> createChildValidator) : Controller
{
    public async Task<IActionResult> Index(Guid? selectedId, CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(selectedId, cancellationToken));

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCanHandleCustomOrders(Guid id, bool value, CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(id, cancellationToken))
        {
            return Forbid();
        }

        await organisationAdminService.SetCanHandleCustomOrdersAsync(id, value, cancellationToken);
        return RedirectToAction(nameof(Index), new { selectedId = id });
    }

    /// <summary>Resource-based check against the target node's own HierarchyPath — see
    /// HierarchyDescendantRequirement. Re-checked here even though the view already hides the
    /// triggering button/form for a user who'd fail it; never trust the hidden-button UX alone.</summary>
    private async Task<bool> CanManageAsync(Guid targetNodeId, CancellationToken cancellationToken)
    {
        var nodes = await organisationAdminService.ListAsync(cancellationToken);
        var target = nodes.FirstOrDefault(n => n.Id == targetNodeId);
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
        // for a DGI Admin, or e.g. Kenya for a Kenya Manager (Kenya's real ParentId points at
        // DGI, but DGI is filtered out of their scoped result, so it's effectively their root).
        var rootId = nodes.First(n => n.ParentId is null || !byId.ContainsKey(n.ParentId.Value)).Id;
        var tree = BuildTree(rootId, byId, byParent);

        var selected = (selectedId.HasValue ? FindNode(tree, selectedId.Value) : null) ?? tree;
        var selectedAdmin = byId[selected.Id];

        var canManage = (await authorizationService.AuthorizeAsync(User, selectedAdmin.HierarchyPath, AuthorizationPolicies.ManageOrgInScope)).Succeeded;

        var validChildLevels = new[] { OrganisationLevel.Country, OrganisationLevel.Intermediate, OrganisationLevel.RetailPoint }
            .Where(level => organisationAdminService.IsValidChildLevel(selectedAdmin.Level, level))
            .Select(level => (Value: level.ToString(), Label: level.ToString()))
            .ToList();

        return new OrganisationsIndexViewModel(tree, selected, canManage, validChildLevels);
    }

    private static OrgNode BuildTree(Guid nodeId, IReadOnlyDictionary<Guid, OrganisationAdminNode> byId, ILookup<Guid?, OrganisationAdminNode> byParent)
    {
        var node = byId[nodeId];
        var children = byParent[nodeId]
            .OrderBy(c => c.Name)
            .Select(c => BuildTree(c.Id, byId, byParent))
            .ToList();

        return new OrgNode(node.Id, node.Name, LevelDisplay(node.Level), node.Kind, node.IsTrainingOrg, node.CanHandleCustomOrders, children);
    }

    private static OrgNode? FindNode(OrgNode node, Guid id) =>
        node.Id == id ? node : node.Children.Select(c => FindNode(c, id)).FirstOrDefault(n => n is not null);

    private static string LevelDisplay(OrganisationLevel level) => level == OrganisationLevel.Dgi ? "DGI" : level.ToString();
}

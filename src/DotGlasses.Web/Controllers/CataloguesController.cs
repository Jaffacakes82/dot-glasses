using System.Globalization;
using DotGlasses.Application.Common;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Enums;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.PresetCatalogueManage)]
public class CataloguesController(
    IPresetCatalogueAdminService catalogueAdminService,
    IOrganisationAdminService organisationAdminService,
    IUserAssignmentsQueryService userAssignmentsQueryService,
    ICurrentUserContext currentUserContext,
    IAuthorizationService authorizationService,
    IUnscopedReportQueryService unscopedReportQueryService,
    IValidator<CreateCatalogueRequest> createValidator,
    IValidator<UpdateCatalogueRequest> updateValidator,
    IValidator<AssignCataloguesRequest> assignValidator) : Controller
{
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(search, cancellationToken));

    /// <summary>The read-only Lens powers page (ticket 08), behind the same policy as the rest of
    /// this screen: every value list and the display format come from
    /// <see cref="LensPowerValues"/> — the single definition the Field App, the Add lens dialog
    /// and the server all read (ADR-0007). The view holds none of these bounds itself.</summary>
    public IActionResult LensPowers() => View(new LensPowersViewModel(
        LensPowerValues.Sphere.Select(LensPowerValues.FormatPower).ToList(),
        LensPowerValues.Cylinder.Select(LensPowerValues.FormatPower).ToList(),
        LensPowerValues.Axis.Select(a => a.ToString("0", CultureInfo.InvariantCulture)).ToList(),
        LensPowerValues.Add.Select(LensPowerValues.FormatPower).ToList()));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCatalogue(CreateCatalogueRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, cancellationToken));
        }

        var owningOrgNodeId = await ResolveOwningOrgNodeIdAsync(request.OwningOrgNodeId, cancellationToken);
        await catalogueAdminService.CreateAsync(request.Name, request.Description, owningOrgNodeId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>The explicit choice if one was posted, otherwise the caller's single qualifying
    /// Dgi/Country assignment — safe to assume there is exactly one because createValidator
    /// already refused a blank choice when more than one qualifies.</summary>
    private async Task<Guid> ResolveOwningOrgNodeIdAsync(Guid? requestedOwningOrgNodeId, CancellationToken cancellationToken)
    {
        if (requestedOwningOrgNodeId is { } chosen)
        {
            return chosen;
        }

        var options = await userAssignmentsQueryService.ListDgiOrCountryAssignmentsAsync(currentUserContext.UserId!.Value, cancellationToken);
        return options.Single().OrgNodeId;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCatalogue(UpdateCatalogueRequest request, CancellationToken cancellationToken)
    {
        if (!await CanEditLensSetAsync(request.Id, cancellationToken))
        {
            return Forbid();
        }

        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, cancellationToken));
        }

        await catalogueAdminService.UpdateAsync(request.Id, request.Name, request.Description, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLensOption(Guid lensOptionId, CancellationToken cancellationToken)
    {
        if (await catalogueAdminService.FindCatalogueIdForLensOptionAsync(lensOptionId, cancellationToken) is not { } catalogueId
            || !await CanEditLensSetAsync(catalogueId, cancellationToken))
        {
            return Forbid();
        }

        await catalogueAdminService.RemoveLensOptionAsync(lensOptionId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignCatalogues(AssignCataloguesRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAssignToAsync(request.OrgNodeId, cancellationToken))
        {
            return Forbid();
        }

        var validationResult = await assignValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, cancellationToken));
        }

        foreach (var catalogueId in request.CatalogueIds)
        {
            await catalogueAdminService.AssignCatalogueToOrgAsync(catalogueId, request.OrgNodeId, cancellationToken);
        }

        TempData["Info"] = request.CatalogueIds.Count == 1 ? "Lens set assigned." : $"{request.CatalogueIds.Count} lens sets assigned.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetireCatalogue(Guid catalogueId, CancellationToken cancellationToken)
    {
        if (!await CanEditLensSetAsync(catalogueId, cancellationToken))
        {
            return Forbid();
        }

        await catalogueAdminService.RetireAsync(catalogueId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateCatalogue(Guid catalogueId, CancellationToken cancellationToken)
    {
        if (!await CanEditLensSetAsync(catalogueId, cancellationToken))
        {
            return Forbid();
        }

        await catalogueAdminService.ReactivateAsync(catalogueId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignCatalogue(Guid catalogueId, Guid orgNodeId, CancellationToken cancellationToken)
    {
        if (!await CanAssignToAsync(orgNodeId, cancellationToken))
        {
            return Forbid();
        }

        await catalogueAdminService.UnassignCatalogueFromOrgAsync(catalogueId, orgNodeId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    // --- Lens set permissions (ADR-0005) -------------------------------------------------------
    //
    // Both checks resolve an org's path through IUnscopedReportQueryService: a lens set's owning
    // org usually sits *above* the caller, which a scoped query can't see (CLAUDE.md's standing
    // gotcha). A path that doesn't resolve — an unknown or deactivated org — is refused.

    /// <summary>Edit, lens powers, retire and reactivate: the caller must be at or above the lens
    /// set's owning org. Retired lens sets are included so Reactivate can be checked too.</summary>
    private async Task<bool> CanEditLensSetAsync(Guid catalogueId, CancellationToken cancellationToken) =>
        await catalogueAdminService.FindOwningOrgNodeIdAsync(catalogueId, cancellationToken) is { } owningOrgNodeId
        && await IsAuthorizedAtAsync(owningOrgNodeId, AuthorizationPolicies.PresetCatalogueEditInScope, await OrgPathsAsync(cancellationToken));

    /// <summary>Assign/unassign: the org must be at or below the caller. Which lens set doesn't
    /// matter — any active one may be assigned within the caller's own part of the tree.</summary>
    private async Task<bool> CanAssignToAsync(Guid orgNodeId, CancellationToken cancellationToken) =>
        await IsAuthorizedAtAsync(orgNodeId, AuthorizationPolicies.PresetCatalogueAssignInScope, await OrgPathsAsync(cancellationToken));

    private async Task<bool> IsAuthorizedAtAsync(Guid orgNodeId, string policy, IReadOnlyDictionary<Guid, string> orgPaths) =>
        orgPaths.TryGetValue(orgNodeId, out var path)
        && (await authorizationService.AuthorizeAsync(User, path, policy)).Succeeded;

    private async Task<IReadOnlyDictionary<Guid, string>> OrgPathsAsync(CancellationToken cancellationToken) =>
        (await unscopedReportQueryService.GetOrganisationNodePathsUnscopedAsync(cancellationToken))
            .ToDictionary(x => x.Id, x => x.HierarchyPath);

    private async Task<CataloguesIndexViewModel> BuildViewModelAsync(string? search, CancellationToken cancellationToken)
    {
        var catalogues = await catalogueAdminService.ListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(search))
        {
            // In-memory filter, not a DB-level Where — lens sets number in the tens at most, and
            // ListAsync already loads every one every request; pushing this to SQL would add
            // complexity with no real benefit at this volume.
            catalogues = catalogues.Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        var orgs = await organisationAdminService.ListAsync(cancellationToken);

        var assignableOrgs = orgs
            .Where(o => o.Level is OrganisationLevel.Intermediate or OrganisationLevel.RetailPoint)
            .OrderBy(o => o.Name)
            .Select(o => (o.Id, o.Name))
            .ToList();

        var owningOrgOptions = (await userAssignmentsQueryService.ListDgiOrCountryAssignmentsAsync(currentUserContext.UserId!.Value, cancellationToken))
            .Select(o => (Id: o.OrgNodeId, o.Name))
            .ToList();

        // Actions the caller would be refused aren't offered — the server re-checks every one
        // regardless (CLAUDE.md: hidden-button UX is never the only guard).
        var orgPaths = await OrgPathsAsync(cancellationToken);
        var catalogueCards = new List<CatalogueCard>();
        foreach (var c in catalogues)
        {
            var assignedOrgCards = new List<AssignedOrgCard>();
            foreach (var a in await catalogueAdminService.ListAssignedOrgsAsync(c.Id, cancellationToken))
            {
                assignedOrgCards.Add(new AssignedOrgCard(a.OrgNodeId, a.OrgName,
                    CanUnassign: await IsAuthorizedAtAsync(a.OrgNodeId, AuthorizationPolicies.PresetCatalogueAssignInScope, orgPaths)));
            }

            catalogueCards.Add(new CatalogueCard(
                c.Id, c.Name, c.Description,
                c.LensOptions.Select(LensOptionCard.From).ToList(),
                assignedOrgCards,
                CanEdit: await IsAuthorizedAtAsync(c.OwningOrgNodeId, AuthorizationPolicies.PresetCatalogueEditInScope, orgPaths)));
        }

        var retired = new List<RetiredCatalogueCard>();
        foreach (var c in await catalogueAdminService.ListRetiredAsync(cancellationToken))
        {
            retired.Add(new RetiredCatalogueCard(c.Id, c.Name,
                CanReactivate: await IsAuthorizedAtAsync(c.OwningOrgNodeId, AuthorizationPolicies.PresetCatalogueEditInScope, orgPaths)));
        }

        return new CataloguesIndexViewModel(
            catalogueCards,
            retired,
            assignableOrgs,
            owningOrgOptions,
            search);
    }
}

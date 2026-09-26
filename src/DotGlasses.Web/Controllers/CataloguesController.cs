using DotGlasses.Application.Common;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Enums;
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
    IReferenceDataAdminService referenceDataAdminService,
    ICurrentUserContext currentUserContext,
    IAuthorizationService authorizationService,
    IUnscopedReportQueryService unscopedReportQueryService,
    IValidator<CreateCatalogueRequest> createValidator,
    IValidator<UpdateCatalogueRequest> updateValidator,
    IValidator<AddLensOptionRequest> addLensOptionValidator,
    IValidator<AssignCataloguesRequest> assignValidator,
    IValidator<SetCoatingAvailabilityBatchRequest> coatingAvailabilityValidator) : Controller
{
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(search, cancellationToken));

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

        await catalogueAdminService.CreateAsync(request.Name, request.Description, request.RangeDescription, currentUserContext.OrgNodeId!.Value, cancellationToken);
        return RedirectToAction(nameof(Index));
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

        await catalogueAdminService.UpdateAsync(request.Id, request.Name, request.Description, request.RangeDescription, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLensOption(AddLensOptionRequest request, CancellationToken cancellationToken)
    {
        if (!await CanEditLensSetAsync(request.CatalogueId, cancellationToken))
        {
            return Forbid();
        }

        var validationResult = await addLensOptionValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, cancellationToken));
        }

        await catalogueAdminService.AddLensOptionAsync(request.CatalogueId, request.LensStrengthRefId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLensOption(Guid lensOptionId, CancellationToken cancellationToken)
    {
        var lensSet = (await catalogueAdminService.ListAsync(cancellationToken)).FirstOrDefault(c => c.LensOptions.Any(l => l.Id == lensOptionId));
        if (lensSet is null || !await CanEditLensSetAsync(lensSet.Id, cancellationToken))
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

    /// <summary>The coating-availability grid posts its whole checked-set in one request (see
    /// SetCoatingAvailabilityBatchRequest) rather than one request per cell — this diffs the
    /// submitted set against what's currently available and only writes the cells that actually
    /// changed.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCoatingAvailability(SetCoatingAvailabilityBatchRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await coatingAvailabilityValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, cancellationToken));
        }

        var selectedPairs = new HashSet<(Guid LensStrengthRefId, Guid CoatingRefId)>();
        foreach (var raw in request.Selected)
        {
            if (SetCoatingAvailabilityBatchRequest.TryParsePair(raw, out var lensStrengthRefId, out var coatingRefId))
            {
                selectedPairs.Add((lensStrengthRefId, coatingRefId));
            }
        }

        var (lensStrengths, coatings) = await GetLensStrengthAndCoatingOptionsAsync(cancellationToken);
        foreach (var lensStrength in lensStrengths)
        {
            var currentlyAvailable = (await catalogueAdminService.ListAvailableCoatingsAsync(lensStrength.Id, cancellationToken)).ToHashSet();
            foreach (var coating in coatings)
            {
                var shouldBeAvailable = selectedPairs.Contains((lensStrength.Id, coating.Id));
                var isCurrentlyAvailable = currentlyAvailable.Contains(coating.Id);
                if (shouldBeAvailable && !isCurrentlyAvailable)
                {
                    await catalogueAdminService.AddAvailableCoatingAsync(lensStrength.Id, coating.Id, cancellationToken);
                }
                else if (!shouldBeAvailable && isCurrentlyAvailable)
                {
                    await catalogueAdminService.RemoveAvailableCoatingAsync(lensStrength.Id, coating.Id, cancellationToken);
                }
            }
        }

        return RedirectToAction(nameof(Index));
    }

    // --- Lens set permissions (ADR-0005) -------------------------------------------------------
    //
    // Both checks resolve an org's path through IUnscopedReportQueryService: a lens set's owning
    // org usually sits *above* the caller, which a scoped query can't see (CLAUDE.md's standing
    // gotcha). A path that doesn't resolve — an unknown or deactivated org — is refused.

    /// <summary>Edit, lens powers, retire and reactivate: the caller must be at or above the lens
    /// set's owning org. Retired lens sets are included so Reactivate can be checked too.</summary>
    private async Task<bool> CanEditLensSetAsync(Guid catalogueId, CancellationToken cancellationToken)
    {
        var lensSet = (await catalogueAdminService.ListAsync(cancellationToken))
            .Concat(await catalogueAdminService.ListRetiredAsync(cancellationToken))
            .FirstOrDefault(c => c.Id == catalogueId);

        return lensSet is not null && await IsAuthorizedAtAsync(
            lensSet.OwningOrgNodeId, AuthorizationPolicies.PresetCatalogueEditInScope, await OrgPathsAsync(cancellationToken));
    }

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

    private async Task<(IReadOnlyList<(Guid Id, string Label)> LensStrengths, IReadOnlyList<(Guid Id, string Label)> Coatings)> GetLensStrengthAndCoatingOptionsAsync(CancellationToken cancellationToken)
    {
        var referenceItems = await referenceDataAdminService.ListAllAsync(cancellationToken);
        var lensStrengths = referenceItems.Where(x => x.Category == ReferenceDataCategory.LensStrength && x.IsActive).OrderBy(x => x.SortOrder).Select(x => (x.Id, x.Label)).ToList();
        var coatings = referenceItems.Where(x => x.Category == ReferenceDataCategory.Coating && x.IsActive).OrderBy(x => x.SortOrder).Select(x => (x.Id, x.Label)).ToList();
        return (lensStrengths, coatings);
    }

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
        var (lensStrengths, coatings) = await GetLensStrengthAndCoatingOptionsAsync(cancellationToken);

        var availableCoatingsByStrength = new Dictionary<Guid, IReadOnlyList<Guid>>();
        foreach (var strength in lensStrengths)
        {
            availableCoatingsByStrength[strength.Id] = await catalogueAdminService.ListAvailableCoatingsAsync(strength.Id, cancellationToken);
        }

        var assignableOrgs = orgs
            .Where(o => o.Level is OrganisationLevel.Intermediate or OrganisationLevel.RetailPoint)
            .OrderBy(o => o.Name)
            .Select(o => (o.Id, o.Name))
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
                c.Id, c.Name, c.Description, c.RangeDescription,
                c.LensOptions.Select(l => new LensOptionCard(l.Id, l.LensStrengthRefId, l.Label, l.SortOrder)).ToList(),
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
            lensStrengths,
            coatings,
            availableCoatingsByStrength,
            assignableOrgs,
            search);
    }
}

using DotGlasses.Application.Common;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
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

        await catalogueAdminService.CreateAsync(request.Name, request.Description, request.RangeDescription, currentUserContext.OrgNodeId!.Value, request.Kind, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCatalogue(UpdateCatalogueRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, cancellationToken));
        }

        await catalogueAdminService.UpdateAsync(request.Id, request.Name, request.Description, request.RangeDescription, request.Kind, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLensOption(AddLensOptionRequest request, CancellationToken cancellationToken)
    {
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
        await catalogueAdminService.RemoveLensOptionAsync(lensOptionId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignCatalogues(AssignCataloguesRequest request, CancellationToken cancellationToken)
    {
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
    public async Task<IActionResult> UnassignCatalogue(Guid catalogueId, Guid orgNodeId, CancellationToken cancellationToken)
    {
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
            // In-memory filter, not a DB-level Where — catalogue count is structurally small
            // (at most one SixLensSet, one NineLensSet, any number of Other), so ListAsync
            // already loads every catalogue every request; pushing this to SQL would add
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

        var catalogueCards = new List<CatalogueCard>();
        foreach (var c in catalogues)
        {
            var assignedOrgs = await catalogueAdminService.ListAssignedOrgsAsync(c.Id, cancellationToken);
            catalogueCards.Add(new CatalogueCard(
                c.Id, c.Name, c.Description, c.RangeDescription, c.Kind,
                c.LensOptions.Select(l => new LensOptionCard(l.Id, l.LensStrengthRefId, l.Label, l.SortOrder)).ToList(),
                assignedOrgs.Select(a => new AssignedOrgCard(a.OrgNodeId, a.OrgName)).ToList()));
        }

        return new CataloguesIndexViewModel(
            catalogueCards,
            lensStrengths,
            coatings,
            availableCoatingsByStrength,
            assignableOrgs,
            search);
    }
}

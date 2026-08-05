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
    IValidator<SetCoatingAvailabilityRequest> coatingAvailabilityValidator) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCatalogue(CreateCatalogueRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(cancellationToken));
        }

        await catalogueAdminService.CreateAsync(request.Name, request.Description, request.RangeDescription, currentUserContext.OrgNodeId!.Value, cancellationToken);
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
            return View(nameof(Index), await BuildViewModelAsync(cancellationToken));
        }

        await catalogueAdminService.UpdateAsync(request.Id, request.Name, request.Description, request.RangeDescription, cancellationToken);
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
            return View(nameof(Index), await BuildViewModelAsync(cancellationToken));
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
            return View(nameof(Index), await BuildViewModelAsync(cancellationToken));
        }

        foreach (var catalogueId in request.CatalogueIds)
        {
            await catalogueAdminService.AssignCatalogueToOrgAsync(catalogueId, request.OrgNodeId, cancellationToken);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCoatingAvailability(SetCoatingAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await coatingAvailabilityValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(cancellationToken));
        }

        if (request.Available)
        {
            await catalogueAdminService.AddAvailableCoatingAsync(request.LensStrengthRefId, request.CoatingRefId, cancellationToken);
        }
        else
        {
            await catalogueAdminService.RemoveAvailableCoatingAsync(request.LensStrengthRefId, request.CoatingRefId, cancellationToken);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<CataloguesIndexViewModel> BuildViewModelAsync(CancellationToken cancellationToken)
    {
        var catalogues = await catalogueAdminService.ListAsync(cancellationToken);
        var orgs = await organisationAdminService.ListAsync(cancellationToken);
        var referenceItems = await referenceDataAdminService.ListAllAsync(cancellationToken);

        var lensStrengths = referenceItems.Where(x => x.Category == ReferenceDataCategory.LensStrength && x.IsActive).OrderBy(x => x.SortOrder).ToList();
        var coatings = referenceItems.Where(x => x.Category == ReferenceDataCategory.Coating && x.IsActive).OrderBy(x => x.SortOrder).ToList();

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

        var catalogueCards = catalogues.Select(c => new CatalogueCard(
            c.Id, c.Name, c.Description, c.RangeDescription,
            c.LensOptions.Select(l => new LensOptionCard(l.Id, l.LensStrengthRefId, l.Label, l.SortOrder)).ToList(),
            c.AssignedOrgCount)).ToList();

        return new CataloguesIndexViewModel(
            catalogueCards,
            lensStrengths.Select(x => (x.Id, x.Label)).ToList(),
            coatings.Select(x => (x.Id, x.Label)).ToList(),
            availableCoatingsByStrength,
            assignableOrgs);
    }
}

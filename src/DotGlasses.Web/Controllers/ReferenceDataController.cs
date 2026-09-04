using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Enums;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReferenceDataManage)]
public class ReferenceDataController(
    IReferenceDataAdminService referenceDataAdminService,
    IValidator<CreateReferenceDataItemRequest> createValidator,
    IValidator<UpdateReferenceDataItemRequest> updateValidator) : Controller
{
    /// <summary>Display name/scope-note copy per category, and whether its Create form should
    /// show the image-URL field (only Frame colour, per the CEO's ask for a swatch photo).
    /// Ordering here is display order on the screen.</summary>
    private static readonly (ReferenceDataCategory Category, string Name, string ScopeNote, bool ShowImageField)[] CategoryMeta =
    [
        (ReferenceDataCategory.ReasonNotPurchased, "Reasons not purchased", "DGI-editable · shown in the field app Lead form", false),
        (ReferenceDataCategory.ReferralReason, "Referral reasons", "DGI-editable · shown when a Test is marked Referred", false),
        (ReferenceDataCategory.Coating, "Coatings & tints", "DGI-editable · Lead coating preference and Sale tint/coating checkboxes", false),
        (ReferenceDataCategory.FrameColour, "Frame colors", "DGI-editable · Sale/custom color swatches, matches e-commerce site", true),
        (ReferenceDataCategory.HardCaseColour, "Hard case colors", "DGI-editable · shown when a Sale includes a hard case", false),
        (ReferenceDataCategory.Occupation, "Occupations", "DGI-editable · optional occupation field on Test, Lead and Sale", false),
        (ReferenceDataCategory.LensStrength, "Lens strengths", "DGI-editable · curated power labels used to build Preset Catalogues — see Preset Catalogues for per-strength coating availability", false),
        (ReferenceDataCategory.LensType, "Lens types", "DGI-editable · asked on a custom lens carrying two distinct powers", false),
    ];

    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReferenceDataItemRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(cancellationToken));
        }

        await referenceDataAdminService.CreateAsync(request.Category, request.Label, request.ImageUrl, request.IsOtherOption, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateReferenceDataItemRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(cancellationToken));
        }

        await referenceDataAdminService.UpdateAsync(request.Id, request.Label, request.ImageUrl, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveUp(Guid id, CancellationToken cancellationToken)
    {
        await referenceDataAdminService.MoveUpAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveDown(Guid id, CancellationToken cancellationToken)
    {
        await referenceDataAdminService.MoveDownAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await referenceDataAdminService.DeactivateAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        await referenceDataAdminService.ReactivateAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<ReferenceDataList>> BuildViewModelAsync(CancellationToken cancellationToken)
    {
        var items = await referenceDataAdminService.ListAllAsync(cancellationToken);

        return CategoryMeta.Select(meta =>
        {
            var categoryItems = items.Where(x => x.Category == meta.Category).ToList();
            return new ReferenceDataList(
                meta.Category,
                meta.Name,
                meta.ScopeNote,
                meta.ShowImageField,
                categoryItems.Any(x => x.IsActive && x.IsOtherOption),
                categoryItems.Where(x => x.IsActive).Select(x => new ReferenceDataOption(x.Id, x.Label, x.ImageUrl)).ToList(),
                categoryItems.Where(x => !x.IsActive).Select(x => new ReferenceDataOption(x.Id, x.Label, x.ImageUrl)).ToList());
        }).ToList();
    }
}

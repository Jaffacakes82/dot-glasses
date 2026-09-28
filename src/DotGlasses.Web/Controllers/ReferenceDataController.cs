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
        (ReferenceDataCategory.LensType, "Lens types", "DGI-editable · asked when a lens has an add, on a custom prescription or a lens set lens", false),
    ];

    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReferenceDataItemRequest request, CancellationToken cancellationToken)
    {
        // ModelState as well as the validator: MVC binds a category number the enum doesn't define
        // as a binding error and leaves Category at its default, Occupation, which the validator
        // would then accept — so a page still showing the retired Lens strengths card (category 6,
        // ADR-0007) would otherwise add its item to Occupations.
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid || !ModelState.IsValid)
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

    /// <summary>See ADR-0001. AddCoatingExclusionAsync throws DomainRuleViolationException for
    /// every validation failure (self-exclusion, retired/wrong-category coating, duplicate rule, or
    /// a lens set lens that pairs the two coatings) — surfaced on this screen by
    /// DomainRuleViolationFilter, not caught here (ADR-0003). Pairings are not managed here any
    /// more: they belong to each lens set lens (ADR-0007).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCoatingExclusion(Guid coatingRefIdA, Guid coatingRefIdB, CancellationToken cancellationToken)
    {
        await referenceDataAdminService.AddCoatingExclusionAsync(coatingRefIdA, coatingRefIdB, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCoatingExclusion(Guid id, CancellationToken cancellationToken)
    {
        await referenceDataAdminService.RemoveCoatingExclusionAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<IReadOnlyList<ReferenceDataList>> BuildViewModelAsync(CancellationToken cancellationToken)
    {
        var items = await referenceDataAdminService.ListAllAsync(cancellationToken);
        var exclusions = await referenceDataAdminService.ListCoatingExclusionsAsync(cancellationToken);

        return CategoryMeta.Select(meta =>
        {
            var categoryItems = items.Where(x => x.Category == meta.Category).ToList();
            var isCoating = meta.Category == ReferenceDataCategory.Coating;
            return new ReferenceDataList(
                meta.Category,
                meta.Name,
                meta.ScopeNote,
                meta.ShowImageField,
                categoryItems.Any(x => x.IsActive && x.IsOtherOption),
                categoryItems.Where(x => x.IsActive).Select(x => new ReferenceDataOption(x.Id, x.Label, x.ImageUrl)).ToList(),
                categoryItems.Where(x => !x.IsActive).Select(x => new ReferenceDataOption(x.Id, x.Label, x.ImageUrl)).ToList(),
                isCoating ? exclusions : []);
        }).ToList();
    }
}

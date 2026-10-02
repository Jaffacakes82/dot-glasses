using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Enums;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using DotGlasses.Web.Validation.ReferenceData;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReferenceDataManage)]
public class ReferenceDataController(
    IReferenceDataAdminService referenceDataAdminService,
    IValidator<CreateReferenceDataItemRequest> createValidator,
    IValidator<UpdateReferenceDataItemRequest> updateValidator,
    IReferenceDataPictureStore pictureStore) : Controller
{
    /// <summary>Display name/scope-note copy per category, and whether its forms take a picture
    /// (the two frame colour lists). Ordering here is display order on the screen.</summary>
    private static readonly (ReferenceDataCategory Category, string Name, string ScopeNote, bool ShowImageField)[] CategoryMeta =
    [
        (ReferenceDataCategory.ReasonNotPurchased, "Reasons not purchased", "DGI-editable · shown in the field app Lead form", false),
        (ReferenceDataCategory.ReferralReason, "Referral reasons", "DGI-editable · shown when a Test is marked Referred", false),
        (ReferenceDataCategory.Coating, "Coatings & tints", "DGI-editable · Lead coating preference and Sale tint/coating checkboxes", false),
        (ReferenceDataCategory.FrameColour, "Frame colours (adult)", "DGI-editable · offered on a Sale unless \"children's frame\" is ticked", true),
        (ReferenceDataCategory.FrameColourChild, "Frame colours (child)", "DGI-editable · offered on a Sale when \"children's frame\" is ticked", true),
        (ReferenceDataCategory.HardCaseColour, "Hard case colours", "DGI-editable · shown when a Sale includes a hard case", false),
        (ReferenceDataCategory.Occupation, "Occupations", "DGI-editable · optional occupation field on Test, Lead and Sale", false),
        (ReferenceDataCategory.LensType, "Lens types", "DGI-editable · asked when a lens has an add, on a custom prescription or a lens set lens", false),
    ];

    /// <summary>Well above the 1 MB a picture may be, so a picture that is a little too big is
    /// answered with the validator's message rather than a bare "request too large" — and far
    /// below the framework's default, so nobody can post hundreds of megabytes at these forms.</summary>
    private const long MaxUploadRequestBytes = 8 * 1024 * 1024;

    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadRequestBytes)]
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

        var imageUrl = request.Picture is { } picture ? await StoreAsync(picture, cancellationToken) : null;
        try
        {
            await referenceDataAdminService.CreateAsync(request.Category, request.Label, imageUrl, request.IsOtherOption, cancellationToken);
        }
        catch
        {
            // The item wasn't saved, so nothing points at the picture just stored.
            await DeleteStoredAsync(imageUrl, CancellationToken.None);
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    public async Task<IActionResult> Update(UpdateReferenceDataItemRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(cancellationToken));
        }

        // A new picture wins over "Remove picture" when both are sent: the admin chose a file.
        ReferenceDataPictureChange change = request.Picture is { } picture
            ? new ReferenceDataPictureChange.Replace(await StoreAsync(picture, cancellationToken))
            : request.RemovePicture ? new ReferenceDataPictureChange.Remove() : new ReferenceDataPictureChange.Keep();

        string? previous;
        try
        {
            previous = await referenceDataAdminService.UpdateAsync(request.Id, request.Label, change, cancellationToken);
        }
        catch
        {
            await DeleteStoredAsync((change as ReferenceDataPictureChange.Replace)?.ImageUrl, CancellationToken.None);
            throw;
        }

        // Only after the item no longer points at it. An old pasted web address has nothing in
        // storage to delete.
        await DeleteStoredAsync(previous, cancellationToken);
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

    /// <summary>Stores an upload the validator has already accepted and returns the address the
    /// item will hold. The type is read again from the bytes rather than carried over from the
    /// validator, so what is stored is described by what it is.</summary>
    private async Task<string> StoreAsync(IFormFile picture, CancellationToken cancellationToken)
    {
        var (type, error) = await ReferenceDataPictureUpload.CheckAsync(picture, cancellationToken);
        if (type is null)
        {
            throw new InvalidOperationException($"An unvalidated picture reached storage: {error}");
        }

        await using var content = picture.OpenReadStream();
        return ReferenceDataPictures.UrlFor(await pictureStore.SaveAsync(content, type, cancellationToken));
    }

    private async Task DeleteStoredAsync(string? imageUrl, CancellationToken cancellationToken)
    {
        if (ReferenceDataPictures.StoredNameOf(imageUrl) is { } name)
        {
            await pictureStore.DeleteAsync(name, cancellationToken);
        }
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

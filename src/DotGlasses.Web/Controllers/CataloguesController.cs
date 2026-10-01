using System.Globalization;
using DotGlasses.Application.Common;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Common;
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
    IValidator<AssignCataloguesRequest> assignValidator,
    IValidator<SaveLensRequest> saveLensValidator,
    IReferenceDataSnapshotProvider referenceDataSnapshotProvider) : Controller
{
    /// <summary>The "Lens sets" tab: one lens set on screen at a time. <paramref name="catalogueId"/>
    /// is the one chosen in the picker; every write on this screen redirects back here carrying
    /// it, so the admin stays on the lens set they were working on.</summary>
    public async Task<IActionResult> Index(string? search, Guid? catalogueId, CancellationToken cancellationToken) =>
        View(await BuildViewModelAsync(search, catalogueId, cancellationToken));

    /// <summary>The read-only "Lens powers" tab, behind the same policy as the rest of this
    /// screen: every value list, its range summary and the display format come from
    /// <see cref="LensPowerValues"/> — the single definition the Field App, the Add lens dialog
    /// and the server all read (ADR-0007). The view holds none of these bounds itself.
    /// <paramref name="catalogueId"/> is only carried through, so the "Lens sets" tab opens back
    /// on the lens set the admin came from.</summary>
    public IActionResult LensPowers(Guid? catalogueId)
    {
        static string Whole(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);

        return View(new LensPowersViewModel(
            [
                LensPowerList.From("Sphere", "lens-power-sphere", LensPowerValues.Sphere, LensPowerValues.SphereRange, LensPowerValues.FormatPower, "required on every lens"),
                LensPowerList.From("Cylinder", "lens-power-cylinder", LensPowerValues.Cylinder, LensPowerValues.CylinderRange, LensPowerValues.FormatPower, "blank means 0.00; the shop sells no positive cylinder"),
                LensPowerList.From("Axis", "lens-power-axis", LensPowerValues.Axis, LensPowerValues.AxisRange, Whole, "whole degrees, asked only when the cylinder isn't 0.00"),
                LensPowerList.From("Add", "lens-power-add", LensPowerValues.Add, LensPowerValues.AddRange, LensPowerValues.FormatPower, "blank or 0.00 means no add"),
                LensPowerList.From("Pupil distance", "lens-power-pupil-distance", LensPowerValues.PupilDistanceMm, LensPowerValues.PupilDistanceMmRange, Whole, "whole millimetres, one value for the pair on a Custom prescription"),
            ],
            catalogueId));
    }

    /// <summary>Back to the "Lens sets" tab on this lens set (POST-redirect-GET). Null — nothing
    /// to return to, e.g. the set was just retired — opens on the first one.</summary>
    private RedirectToActionResult ToLensSet(Guid? catalogueId) =>
        catalogueId is { } id ? RedirectToAction(nameof(Index), new { catalogueId = id }) : RedirectToAction(nameof(Index));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCatalogue(CreateCatalogueRequest request, Guid? selectedCatalogueId, CancellationToken cancellationToken)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, selectedCatalogueId, cancellationToken));
        }

        var owningOrgNodeId = await ResolveOwningOrgNodeIdAsync(request.OwningOrgNodeId, cancellationToken);
        var created = await catalogueAdminService.CreateAsync(request.Name, request.Description, owningOrgNodeId, cancellationToken);

        // The new lens set is the one to work on next: it has no lenses yet.
        return ToLensSet(created.Id);
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
            return View(nameof(Index), await BuildViewModelAsync(null, request.Id, cancellationToken));
        }

        await catalogueAdminService.UpdateAsync(request.Id, request.Name, request.Description, cancellationToken);
        return ToLensSet(request.Id);
    }

    /// <summary>
    /// The Add lens dialog's save, for a new lens and for an edit alike (ADR-0007).
    ///
    /// <para>
    /// The round trip. A save that passes is written and redirected back to Index — on the same
    /// lens set — with a banner (POST-redirect-GET). A refused one comes back the way every other refused form on this
    /// screen does (CreateCatalogue, UpdateCatalogue, AssignCatalogues): the screen is rendered
    /// straight from this POST with the validator's failures in ModelState — no redirect. What
    /// that adds here is the admin's own posted form, handed to the view as
    /// <see cref="LensDialogViewModel.Reopen"/>, so _LensDialog renders open on their input with
    /// each problem in the slot for the field it is keyed on (the request's property names are
    /// the dialog's field names). Rendering rather than redirecting is deliberate: a redirect
    /// would have to carry the whole form, pairing rows and every keyed message through TempData
    /// to rebuild exactly this. Nothing has been written when it renders, so reading the memoised
    /// snapshot for the page is safe.
    /// </para>
    ///
    /// <para>
    /// A business-rule rejection (the lens set retired, or the lens removed, since the page was
    /// loaded) goes through DomainRuleViolationFilter's POST-redirect-GET like every other
    /// screen's.
    /// </para>
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLens(SaveLensRequest request, CancellationToken cancellationToken)
    {
        if (!await CanEditLensSetAsync(request.CatalogueId, cancellationToken))
        {
            return Forbid();
        }

        if (request.LensOptionId is { } lensOptionId)
        {
            // A lens is only ever edited through its own lens set — the permission above was
            // checked against that set, not whichever one the lens really sits in.
            var owningCatalogueId = await catalogueAdminService.FindCatalogueIdForLensOptionAsync(lensOptionId, cancellationToken)
                ?? throw new DomainRuleViolationException("This lens is no longer in the lens set — it may have been removed. Add it again if it is still needed.");
            if (owningCatalogueId != request.CatalogueId)
            {
                return Forbid();
            }
        }

        var validationResult = await saveLensValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid || !ModelState.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, request.CatalogueId, cancellationToken, reopenLensDialog: request));
        }

        await catalogueAdminService.SaveLensAsync(request.CatalogueId, request.LensOptionId, request.ToInput(), cancellationToken);
        TempData["Info"] = request.LensOptionId is null ? $"Lens \"{request.Label!.Trim()}\" added." : $"Lens \"{request.Label!.Trim()}\" saved.";
        return ToLensSet(request.CatalogueId);
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
        return ToLensSet(catalogueId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignCatalogues(AssignCataloguesRequest request, Guid? selectedCatalogueId, CancellationToken cancellationToken)
    {
        if (!await CanAssignToAsync(request.OrgNodeId, cancellationToken))
        {
            return Forbid();
        }

        var validationResult = await assignValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildViewModelAsync(null, selectedCatalogueId, cancellationToken));
        }

        foreach (var catalogueId in request.CatalogueIds)
        {
            await catalogueAdminService.AssignCatalogueToOrgAsync(catalogueId, request.OrgNodeId, cancellationToken);
        }

        TempData["Info"] = request.CatalogueIds.Count == 1 ? "Lens set assigned." : $"{request.CatalogueIds.Count} lens sets assigned.";
        return ToLensSet(selectedCatalogueId);
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

        // The retired lens set is no longer in the picker, so there is no selection to keep.
        return ToLensSet(null);
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
        return ToLensSet(catalogueId);
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
        return ToLensSet(catalogueId);
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

    /// <summary>
    /// The screen shows one lens set at a time (prototype variant A): <paramref name="selectedCatalogueId"/>
    /// if it is one of the lens sets on offer, otherwise the first. Only that one is built in full —
    /// the rest are just names for the picker and the assign form.
    /// </summary>
    private async Task<CataloguesIndexViewModel> BuildViewModelAsync(
        string? search, Guid? selectedCatalogueId, CancellationToken cancellationToken, SaveLensRequest? reopenLensDialog = null)
    {
        var allCatalogues = await catalogueAdminService.ListAsync(cancellationToken);
        var catalogues = allCatalogues;
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

        // One unscoped read serves both the permission checks (paths) and the owner's name: a lens
        // set's owning org usually sits above the caller, which a scoped query can't see
        // (CLAUDE.md's standing gotcha).
        var orgNodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);
        var orgPaths = orgNodes.ToDictionary(x => x.Id, x => x.HierarchyPath);
        var orgNames = orgNodes.ToDictionary(x => x.Id, x => x.Name);

        // Actions the caller would be refused aren't offered — the server re-checks every one
        // regardless (CLAUDE.md: hidden-button UX is never the only guard).
        CatalogueCard? selected = null;
        if ((catalogues.FirstOrDefault(x => x.Id == selectedCatalogueId) ?? catalogues.FirstOrDefault()) is { } c)
        {
            var assignedOrgCards = new List<AssignedOrgCard>();
            foreach (var a in await catalogueAdminService.ListAssignedOrgsAsync(c.Id, cancellationToken))
            {
                assignedOrgCards.Add(new AssignedOrgCard(a.OrgNodeId, a.OrgName,
                    CanUnassign: await IsAuthorizedAtAsync(a.OrgNodeId, AuthorizationPolicies.PresetCatalogueAssignInScope, orgPaths)));
            }

            selected = new CatalogueCard(
                c.Id, c.Name, c.Description,
                orgNames.GetValueOrDefault(c.OwningOrgNodeId, "Unknown organisation"),
                c.LensOptions.Select(LensOptionCard.From).ToList(),
                assignedOrgCards,
                CanEdit: await IsAuthorizedAtAsync(c.OwningOrgNodeId, AuthorizationPolicies.PresetCatalogueEditInScope, orgPaths));
        }

        var retired = new List<RetiredCatalogueCard>();
        foreach (var r in await catalogueAdminService.ListRetiredAsync(cancellationToken))
        {
            retired.Add(new RetiredCatalogueCard(r.Id, r.Name,
                CanReactivate: await IsAuthorizedAtAsync(r.OwningOrgNodeId, AuthorizationPolicies.PresetCatalogueEditInScope, orgPaths)));
        }

        return new CataloguesIndexViewModel(
            catalogues.Select(x => new LensSetPickerOption(x.Id, x.Name)).ToList(),
            selected,
            retired,
            assignableOrgs,
            owningOrgOptions,
            search,
            await BuildLensDialogAsync(reopenLensDialog, allCatalogues, cancellationToken));
    }

    /// <summary>The dialog's choices come off the memoised snapshot: this is a page render, never
    /// the write path (the validator reads its own rows).</summary>
    private async Task<LensDialogViewModel> BuildLensDialogAsync(
        SaveLensRequest? reopen, IEnumerable<PresetCatalogueAdminDto> catalogues, CancellationToken cancellationToken)
    {
        var referenceData = await referenceDataSnapshotProvider.GetAsync(cancellationToken);
        IReadOnlyList<LensDialogChoice> Active(Contracts.Common.ReferenceDataCategory category) =>
            referenceData.Items
                .Where(i => i.Category == category && i.IsActive)
                .Select(i => new LensDialogChoice(i.Id, i.Label, i.IsOtherOption))
                .ToList();

        var exclusions = referenceData.CoatingExclusions
            .Select(e => $"{referenceData.ResolveLabel(e.CoatingRefIdA)} and {referenceData.ResolveLabel(e.CoatingRefIdB)}")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? title = null;
        if (reopen is not null)
        {
            var lensSetName = catalogues.FirstOrDefault(c => c.Id == reopen.CatalogueId)?.Name;
            title = reopen.LensOptionId is null ? $"Add lens to {lensSetName}" : $"Edit lens in {lensSetName}";
        }

        return new LensDialogViewModel(
            Active(Contracts.Common.ReferenceDataCategory.Coating),
            Active(Contracts.Common.ReferenceDataCategory.LensType),
            exclusions,
            reopen,
            title);
    }
}

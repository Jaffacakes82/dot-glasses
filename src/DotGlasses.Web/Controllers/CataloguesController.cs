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
    IValidator<AssignCatalogueRequest> assignValidator,
    IValidator<SaveLensRequest> saveLensValidator,
    IReferenceDataSnapshotProvider referenceDataSnapshotProvider) : Controller
{
    /// <summary>The "Lens sets" tab: every lens set as a row. <paramref name="status"/> is
    /// "active" (the default, and what anything unrecognised means), "retired" or "all".</summary>
    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken) =>
        View(await BuildListAsync(search, ParseStatus(status), cancellationToken));

    /// <summary>One lens set's own page — its lenses and the orgs it is assigned to. Every write
    /// made from it redirects back here, so the admin stays on the lens set they were working on.
    /// A retired lens set opens read-only; an id that names no lens set goes back to the list.</summary>
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken) =>
        await DetailsViewAsync(id, cancellationToken);

    /// <summary>The read-only "Lens powers" tab, behind the same policy as the rest of this
    /// screen: every value list, its range summary and the display format come from
    /// <see cref="LensPowerValues"/> — the single definition the Field App, the Add lens dialog
    /// and the server all read (ADR-0007). The view holds none of these bounds itself.</summary>
    public IActionResult LensPowers()
    {
        static string Whole(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);

        return View(new LensPowersViewModel(
            [
                LensPowerList.From("Sphere", "lens-power-sphere", LensPowerValues.Sphere, LensPowerValues.SphereRange, LensPowerValues.FormatPower, "required on every lens"),
                LensPowerList.From("Cylinder", "lens-power-cylinder", LensPowerValues.Cylinder, LensPowerValues.CylinderRange, LensPowerValues.FormatPower, "blank means 0.00; the shop sells no positive cylinder"),
                LensPowerList.From("Axis", "lens-power-axis", LensPowerValues.Axis, LensPowerValues.AxisRange, Whole, "whole degrees, asked only when the cylinder isn't 0.00"),
                LensPowerList.From("Add", "lens-power-add", LensPowerValues.Add, LensPowerValues.AddRange, LensPowerValues.FormatPower, "blank or 0.00 means no add"),
                LensPowerList.From("Pupil distance", "lens-power-pupil-distance", LensPowerValues.PupilDistanceMm, LensPowerValues.PupilDistanceMmRange, Whole, "whole millimetres, one value for the pair on a Custom prescription"),
            ]));
    }

    /// <summary>Back to this lens set's page (POST-redirect-GET).</summary>
    private RedirectToActionResult ToLensSet(Guid catalogueId) =>
        RedirectToAction(nameof(Details), new { id = catalogueId });

    /// <summary>Renders a lens set's page — for the GET, and straight from a refused POST with its
    /// failures in ModelState.</summary>
    private async Task<IActionResult> DetailsViewAsync(Guid catalogueId, CancellationToken cancellationToken, SaveLensRequest? reopenLensDialog = null) =>
        await BuildDetailsAsync(catalogueId, cancellationToken, reopenLensDialog) is { } details
            ? View(nameof(Details), details)
            : RedirectToAction(nameof(Index));

    private static LensSetStatusFilter ParseStatus(string? status) =>
        Enum.TryParse<LensSetStatusFilter>(status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : LensSetStatusFilter.Active;

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCatalogue(CreateCatalogueRequest request, CancellationToken cancellationToken)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return View(nameof(Index), await BuildListAsync(null, LensSetStatusFilter.Active, cancellationToken));
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
            return await DetailsViewAsync(request.Id, cancellationToken);
        }

        await catalogueAdminService.UpdateAsync(request.Id, request.Name, request.Description, cancellationToken);
        return ToLensSet(request.Id);
    }

    /// <summary>
    /// The Add lens dialog's save, for a new lens and for an edit alike (ADR-0007).
    ///
    /// <para>
    /// The round trip. A save that passes is written and redirected back to the lens set's page
    /// with a banner (POST-redirect-GET). A refused one comes back the way every other refused form
    /// on these pages does (CreateCatalogue, UpdateCatalogue, AssignCatalogue): the page is rendered
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
            return await DetailsViewAsync(request.CatalogueId, cancellationToken, reopenLensDialog: request);
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
    public async Task<IActionResult> AssignCatalogue(AssignCatalogueRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAssignToAsync(request.OrgNodeId, cancellationToken))
        {
            return Forbid();
        }

        var validationResult = await assignValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            validationResult.AddToModelState(ModelState);
            return await DetailsViewAsync(request.CatalogueId, cancellationToken);
        }

        await catalogueAdminService.AssignCatalogueToOrgAsync(request.CatalogueId, request.OrgNodeId, cancellationToken);

        TempData["Info"] = "Lens set assigned.";
        return ToLensSet(request.CatalogueId);
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

        // Its page is read-only from here on, so back to the list.
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

    private async Task<LensSetsListViewModel> BuildListAsync(string? search, LensSetStatusFilter status, CancellationToken cancellationToken)
    {
        var lensSets = new List<PresetCatalogueAdminDto>();
        if (status is not LensSetStatusFilter.Retired)
        {
            lensSets.AddRange(await catalogueAdminService.ListAsync(cancellationToken));
        }
        if (status is not LensSetStatusFilter.Active)
        {
            lensSets.AddRange(await catalogueAdminService.ListRetiredAsync(cancellationToken));
        }

        // In-memory filter and sort, not a DB-level Where — lens sets number in the tens at most.
        var shown = lensSets
            .Where(c => string.IsNullOrWhiteSpace(search) || c.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var orgs = await OrgNodesAsync(cancellationToken);
        var assignedCounts = await catalogueAdminService.CountAssignedOrgsAsync(cancellationToken);

        var rows = new List<LensSetRow>();
        foreach (var c in shown)
        {
            rows.Add(new LensSetRow(
                c.Id, c.Name, c.Description,
                orgs.Names.GetValueOrDefault(c.OwningOrgNodeId, "Unknown organisation"),
                c.LensOptions.Count,
                assignedCounts.GetValueOrDefault(c.Id),
                c.IsRetired,
                CanReactivate: c.IsRetired && await IsAuthorizedAtAsync(c.OwningOrgNodeId, AuthorizationPolicies.PresetCatalogueEditInScope, orgs.Paths)));
        }

        var owningOrgOptions = (await userAssignmentsQueryService.ListDgiOrCountryAssignmentsAsync(currentUserContext.UserId!.Value, cancellationToken))
            .Select(o => (Id: o.OrgNodeId, o.Name))
            .ToList();

        return new LensSetsListViewModel(rows, status, search, owningOrgOptions);
    }

    /// <summary>Null when no lens set has this id. Actions the caller would be refused aren't
    /// offered — the server re-checks every one regardless (CLAUDE.md: hidden-button UX is never
    /// the only guard) — and a retired lens set offers none but Reactivate.</summary>
    private async Task<LensSetDetailsViewModel?> BuildDetailsAsync(
        Guid catalogueId, CancellationToken cancellationToken, SaveLensRequest? reopenLensDialog = null)
    {
        if (await catalogueAdminService.FindAsync(catalogueId, cancellationToken) is not { } c)
        {
            return null;
        }

        var orgs = await OrgNodesAsync(cancellationToken);
        var mayEdit = await IsAuthorizedAtAsync(c.OwningOrgNodeId, AuthorizationPolicies.PresetCatalogueEditInScope, orgs.Paths);

        var assignedOrgCards = new List<AssignedOrgCard>();
        foreach (var a in await catalogueAdminService.ListAssignedOrgsAsync(c.Id, cancellationToken))
        {
            assignedOrgCards.Add(new AssignedOrgCard(a.OrgNodeId, a.OrgName,
                CanUnassign: !c.IsRetired && await IsAuthorizedAtAsync(a.OrgNodeId, AuthorizationPolicies.PresetCatalogueAssignInScope, orgs.Paths)));
        }

        var lensSet = new CatalogueCard(
            c.Id, c.Name, c.Description,
            orgs.Names.GetValueOrDefault(c.OwningOrgNodeId, "Unknown organisation"),
            c.LensOptions.Select(LensOptionCard.From).ToList(),
            assignedOrgCards.OrderBy(a => a.OrgName, StringComparer.OrdinalIgnoreCase).ToList(),
            CanEdit: mayEdit && !c.IsRetired,
            IsRetired: c.IsRetired,
            CanReactivate: mayEdit && c.IsRetired);

        IReadOnlyList<(Guid Id, string Name)> assignableOrgs = c.IsRetired
            ? []
            : (await organisationAdminService.ListAsync(cancellationToken))
                .Where(o => o.Level is OrganisationLevel.Intermediate or OrganisationLevel.RetailPoint)
                .Where(o => assignedOrgCards.All(a => a.OrgNodeId != o.Id))
                .OrderBy(o => o.Name)
                .Select(o => (o.Id, o.Name))
                .ToList();

        return new LensSetDetailsViewModel(lensSet, assignableOrgs, await BuildLensDialogAsync(reopenLensDialog, c.Name, cancellationToken));
    }

    /// <summary>One unscoped read serves both the permission checks (paths) and the owner's name:
    /// a lens set's owning org usually sits above the caller, which a scoped query can't see
    /// (CLAUDE.md's standing gotcha).</summary>
    private async Task<(IReadOnlyDictionary<Guid, string> Paths, IReadOnlyDictionary<Guid, string> Names)> OrgNodesAsync(CancellationToken cancellationToken)
    {
        var orgNodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);
        return (orgNodes.ToDictionary(x => x.Id, x => x.HierarchyPath), orgNodes.ToDictionary(x => x.Id, x => x.Name));
    }

    /// <summary>The dialog's choices come off the memoised snapshot: this is a page render, never
    /// the write path (the validator reads its own rows).</summary>
    private async Task<LensDialogViewModel> BuildLensDialogAsync(
        SaveLensRequest? reopen, string lensSetName, CancellationToken cancellationToken)
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

        var title = reopen switch
        {
            null => null,
            { LensOptionId: null } => $"Add lens to {lensSetName}",
            _ => $"Edit lens in {lensSetName}",
        };

        return new LensDialogViewModel(
            Active(Contracts.Common.ReferenceDataCategory.Coating),
            Active(Contracts.Common.ReferenceDataCategory.LensType),
            exclusions,
            reopen,
            title);
    }
}

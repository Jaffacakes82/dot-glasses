using DotGlasses.Application.Leads;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Sales;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.ReferenceData;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Common;
using DotGlasses.Rules;
using DotGlasses.Rules.LensRanges;
using DotGlasses.Rules.LensSets;
using DotGlasses.Rules.ReferenceData;
using DotGlasses.Rules.Sales;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

/// <summary>
/// The Admin Portal's equivalent of the Field App's "convert to sale" action on the Leads
/// worklist (see ConsultationForm.razor/Leads.razor) — Event History's Leads tab links here for
/// an unconverted row. Reading/writing Lead needs no special RBAC beyond plain [Authorize]: both
/// ILeadService.GetByIdAsync and ISaleService.CreateAsync go through the standard hierarchy-
/// scoping query filter, so an admin can only ever see/convert leads inside their own subtree —
/// same mechanism Event History itself already relies on.
///
/// The resulting Sale is stamped with the *Lead's own* TechnicianUserId/HierarchyPath, not the
/// admin's — a deliberate deviation from every other "stamp from the caller" write path in this
/// codebase (Test/Lead/Sale creation via the API). The sale is happening at the Lead's outlet;
/// attributing it to wherever the converting admin's own org node sits (which could be DGI root)
/// would corrupt Event History's audit trail and the Dashboard's per-technician/per-outlet
/// rankings exactly the way Phase 1's offline-sync attribution bug does — see CLAUDE.md.
/// </summary>
[Authorize]
public class LeadConversionController(
    ILeadService leadService,
    ISaleService saleService,
    IReferenceDataQueryService referenceDataQueryService,
    IReferenceDataSnapshotProvider referenceDataSnapshotProvider,
    IPresetCatalogueQueryService presetCatalogueQueryService,
    IOrganisationNodeLookup organisationNodeLookup) : Controller
{
    [HttpGet("Leads/Convert/{id:guid}")]
    public async Task<IActionResult> Convert(Guid id, CancellationToken cancellationToken)
    {
        var lead = await leadService.GetByIdAsync(id, cancellationToken);
        if (lead is null)
        {
            return NotFound();
        }

        if (lead.ConvertedFlag)
        {
            TempData["Info"] = "This lead has already been converted into a sale.";
            return RedirectToAction("Index", "EventHistory", new { tab = "leads" });
        }

        // Prefilled from the same seed the POST assembles through, so what the admin is shown is
        // what a straight submit would record — the Coating set seeded from the Lead's Coating
        // preference included (CONTEXT.md).
        var seeded = SaleAssembly.Seed(lead);
        // "Same lens for both eyes" starts ticked, as it does on the Field App.
        var form = new LeadConversionFormModel { ConsentGiven = seeded.ConsentGiven, CoatingRefIds = seeded.CoatingRefIds, SameLensForBothEyes = true };

        // A Lead whose lens set still reaches it but no longer holds one of its lenses asks again —
        // with the set, and whichever lens still matches, pre-selected rather than blank. It starts
        // "Same lens for both eyes" ticked only if its two eyes are the same lens.
        var atLeadsLocation = await SnapshotAtLeadsLocationAsync(lead, cancellationToken);
        if (LensNoLongerInSet(lead, atLeadsLocation))
        {
            var (left, right) = LeadsLenses(lead, atLeadsLocation);
            form.LensRange = LensRangeChoice.Format(LensRangeType.LensSet, lead.PresetCatalogueId);
            form.LensLeftId = left?.Id;
            form.LensRightId = right?.Id;
            form.SameLensForBothEyes = EyesAreTheSameLens(lead);
            form.PresetPupilDistanceBucket = lead.PresetPupilDistanceBucket;
            form.ChildrensFrame = lead.ChildrensFrame;
        }

        return View(await BuildViewModelAsync(lead, form, cancellationToken));
    }

    /// <summary>
    /// The coatings a pair of lens set lenses both come in, and the pairings they carry — what the
    /// screen's script asks for whenever the admin changes a lens, so the browser never restates
    /// what <see cref="LensSetLenses.CoatingsFor"/> decides. Both lenses must be in a lens set
    /// reaching the Lead's retail point; anything else answers <c>offered: null</c> ("no pair
    /// chosen — list every coating"), never an error, since half-chosen is a normal state here.
    /// With only <c>left</c> given the one lens stands for both ("Same lens for both eyes").
    /// </summary>
    [HttpGet("Leads/Convert/{id:guid}/coatings")]
    public async Task<IActionResult> Coatings(Guid id, Guid? left, Guid? right, CancellationToken cancellationToken)
    {
        var lead = await leadService.GetByIdAsync(id, cancellationToken);
        if (lead is null)
        {
            return NotFound();
        }

        var atLeadsLocation = await SnapshotAtLeadsLocationAsync(lead, cancellationToken);
        var pair = FindPair(atLeadsLocation, left, right ?? left);
        if (pair is not var (leftLens, rightLens))
        {
            return Json(new { offered = (Guid[]?)null, pairings = Array.Empty<object>() });
        }

        var coatings = LensSetLenses.CoatingsFor(leftLens, rightLens);
        return Json(new
        {
            offered = coatings.Offered,
            pairings = coatings.RequiredPairings.Select(p => new { trigger = p.TriggerCoatingRefId, paired = p.PairedCoatingRefId }),
        });
    }

    [HttpPost("Leads/Convert/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(Guid id, LeadConversionFormModel form, CancellationToken cancellationToken)
    {
        var lead = await leadService.GetByIdAsync(id, cancellationToken);
        if (lead is null)
        {
            return NotFound();
        }

        if (lead.ConvertedFlag)
        {
            TempData["Info"] = "This lead has already been converted into a sale.";
            return RedirectToAction("Index", "EventHistory", new { tab = "leads" });
        }

        // Deactivating a retail point after a Lead was recorded there must stop the conversion,
        // the same way it stops a fresh Sale at the Field App API boundary (ticket 07, spec.md
        // "Recording"). Looked up by IgnoreQueryFilters() specifically because deactivation is
        // exactly what's being asked about — see IOrganisationNodeLookup's doc comment.
        //
        // Only refuse when a node is found *and* it's deactivated. A Lead stamped above retail-
        // point level, or a legacy row whose HierarchyPath matches no org node at all (e.g. ""),
        // has nothing here to be deactivated — FindByHierarchyPathAsync returning null means "no
        // such node", not "the node is gone", so it must not be read as a refusal.
        var retailPoint = await organisationNodeLookup.FindByHierarchyPathAsync(lead.HierarchyPath, cancellationToken);
        if (retailPoint is { IsActive: false })
        {
            throw new DomainRuleViolationException($"{retailPoint.Name} has been deactivated.");
        }

        // Placed at the *Lead's* location: the Sale inherits the Lead's attribution (ADR-0005).
        var atLeadsLocation = await SnapshotAtLeadsLocationAsync(lead, cancellationToken);

        form.ApplyLensRange(atLeadsLocation);
        var answers = BuildSaleAnswers(lead, form, LeadLensCarriesOver(lead, atLeadsLocation));

        // No default lens range (ADR-0005) — the same rule the Field App asks through.
        if (SaleAssembly.LensRangeNotChosen(answers) is { } notChosen)
        {
            ModelState.AddModelError($"{nameof(form)}.{notChosen.Key}", notChosen.Message);
            return View(await BuildViewModelAsync(lead, form, cancellationToken));
        }

        var request = SaleAssembly.Build(Guid.NewGuid(), lead.Id, answers);

        // The same module SalesController checks against, off the same per-request snapshot
        // BuildViewModelAsync already loads. The source-Lead check the two API endpoints also run
        // has nothing to do here: it asks whether this Lead is already converted, and the
        // ConvertedFlag guard above has answered that with friendlier copy — SaleService sets
        // ConvertedFlag and SaleId together in one transaction, so the two can't disagree.
        var rules = ConsultationRules.Check(request, atLeadsLocation);
        if (!rules.IsValid)
        {
            // Failures come back keyed by CreateSaleRequest's own property names — LeadConversionFormModel
            // deliberately mirrors those names 1:1 so a straight "Form.{PropertyName}" remap is enough,
            // no per-field translation table needed.
            foreach (var failure in rules.Failures)
            {
                ModelState.AddModelError($"{nameof(form)}.{failure.Key}", failure.Message);
            }

            return View(await BuildViewModelAsync(lead, form, cancellationToken));
        }

        await saleService.CreateAsync(request, lead.TechnicianUserId, lead.HierarchyPath, cancellationToken);

        TempData["Info"] = "Lead converted into a sale.";
        return RedirectToAction("Index", "EventHistory", new { tab = "leads" });
    }

    private async Task<ReferenceDataSnapshot> SnapshotAtLeadsLocationAsync(LeadDto lead, CancellationToken cancellationToken) =>
        (await referenceDataSnapshotProvider.GetAsync(cancellationToken)).AtLocation(lead.HierarchyPath);

    /// <summary>
    /// Whether the Lead's own lens preference carries over onto the Sale: it recorded one, and —
    /// for a lens set — that set is still available at the Lead's retail point and still holds
    /// both its lenses. When it isn't, nothing is substituted (SaleAssembly.Seed is a seed, not a
    /// choice); the screen says why and asks for a lens range instead of showing a summary the
    /// admin can't act on.
    /// </summary>
    private static bool LeadLensCarriesOver(LeadDto lead, ReferenceDataSnapshot atLeadsLocation) =>
        SaleAssembly.CarriesLens(lead)
        && (lead.LensRangeType is not LensRangeType.LensSet
            || (atLeadsLocation.IsLensSetAvailable(lead.PresetCatalogueId) && LeadsLenses(lead, atLeadsLocation) is ({ }, { })));

    /// <summary>A lens-set Lead whose set still reaches its retail point but no longer holds one
    /// (or both) of its lenses — removed or changed since the Lead was captured.</summary>
    private static bool LensNoLongerInSet(LeadDto lead, ReferenceDataSnapshot atLeadsLocation) =>
        lead.LensRangeType is LensRangeType.LensSet
        && atLeadsLocation.IsLensSetAvailable(lead.PresetCatalogueId)
        && !LeadLensCarriesOver(lead, atLeadsLocation);

    /// <summary>The lenses in the Lead's own lens set matching each eye's recorded power and the
    /// Lead's lens type — a record holds no lens id (ADR-0007), so this is how the lens is known.
    /// Null for an eye whose lens has since gone from the set.</summary>
    private static (LensOptionSnapshot? Left, LensOptionSnapshot? Right) LeadsLenses(LeadDto lead, ReferenceDataSnapshot referenceData)
    {
        var lenses = referenceData.FindCatalogue(lead.PresetCatalogueId)?.LensOptions ?? [];
        return (
            LensSetLenses.Match(lenses, lead.SphereLeft, lead.CylinderLeft, lead.AxisLeft, lead.AddLeft, lead.LensTypeRefId),
            LensSetLenses.Match(lenses, lead.SphereRight, lead.CylinderRight, lead.AxisRight, lead.AddRight, lead.LensTypeRefId));
    }

    /// <summary>Whether the Lead's two eyes are the same lens — the same power and lens type, read the
    /// way LensSetLenses.Match reads them (a blank cylinder is 0.00, an axis counts only with a
    /// cylinder, an add of 0.00 is none), so "the same lens" has one definition. Two eyes with
    /// nothing recorded count as matching.</summary>
    private static bool EyesAreTheSameLens(LeadDto lead)
    {
        if (lead.SphereLeft is not { } sphereLeft)
        {
            return lead.SphereRight is null;
        }

        var leftAsLens = new LensOptionSnapshot(Guid.Empty, string.Empty, sphereLeft, [], lead.CylinderLeft, lead.AxisLeft, lead.AddLeft, lead.LensTypeRefId);
        return LensSetLenses.Match([leftAsLens], lead.SphereRight, lead.CylinderRight, lead.AxisRight, lead.AddRight, lead.LensTypeRefId) is not null;
    }

    /// <summary>The two lenses in lens sets reaching the Lead's retail point, or null when either
    /// isn't one. A lens id belongs to exactly one set, so the id alone finds it.</summary>
    private static (LensOptionSnapshot Left, LensOptionSnapshot Right)? FindPair(ReferenceDataSnapshot atLeadsLocation, Guid? leftId, Guid? rightId)
    {
        LensOptionSnapshot? Find(Guid? id) =>
            atLeadsLocation.PresetCatalogues
                .Where(set => atLeadsLocation.IsLensSetAvailable(set.Id))
                .SelectMany(set => set.LensOptions)
                .FirstOrDefault(lens => lens.Id == id);

        return Find(leftId) is { } left && Find(rightId) is { } right ? (left, right) : null;
    }

    /// <summary>The pair the coatings are scoped by: the Lead's own lenses when its lens carries over,
    /// otherwise the ones the form names (one lens standing for both when "Same lens for both eyes"
    /// is ticked). Null when there is no pair yet — a Custom prescription, no range, or a lens still
    /// to choose.</summary>
    private static (LensOptionSnapshot Left, LensOptionSnapshot Right)? ChosenPair(
        LeadDto lead, LeadConversionFormModel form, ReferenceDataSnapshot atLeadsLocation, bool lensCarriesOver)
    {
        if (lensCarriesOver)
        {
            return lead.LensRangeType is LensRangeType.LensSet && LeadsLenses(lead, atLeadsLocation) is ({ } left, { } right)
                ? (left, right)
                : null;
        }

        var (rangeType, catalogueId) = LensRangeChoice.Parse(form.LensRange);
        if (rangeType is not LensRangeType.LensSet)
        {
            return null;
        }

        // Only lenses of the chosen set count: a lens id posted from another set matches nothing.
        var inSet = atLeadsLocation.FindCatalogue(catalogueId)?.LensOptions ?? [];
        var pair = FindPair(atLeadsLocation, form.LensLeftId, form.SameLensForBothEyes ? form.LensLeftId : form.LensRightId);
        return pair is var (l, r) && inSet.Any(x => x.Id == l.Id) && inSet.Any(x => x.Id == r.Id) ? pair : null;
    }

    /// <summary>The note for each eye of a lens-set Lead whose lens has gone from the set and that the
    /// admin has yet to choose again — "The SPH +3.50 on this Lead is no longer in Readers. Choose a
    /// lens." — the same wording the Field App shows. An eye with no power recorded has nothing to be
    /// "no longer in the set". With "Same lens for both eyes" ticked the one dropdown answers both.</summary>
    private static (string? Left, string? Right) LensNotes(LeadDto lead, LeadConversionFormModel form, ReferenceDataSnapshot atLeadsLocation)
    {
        if (!LensNoLongerInSet(lead, atLeadsLocation) || atLeadsLocation.FindCatalogue(lead.PresetCatalogueId) is not { } lensSet)
        {
            return (null, null);
        }

        var (left, right) = LeadsLenses(lead, atLeadsLocation);
        string? Note(decimal? sphere, decimal? cylinder, decimal? axis, decimal? add, LensOptionSnapshot? found, Guid? chosen) =>
            sphere is { } s && found is null && chosen is null
                ? $"The {LensOptionCard.FormatLensPower(s, cylinder, axis, add)} on this Lead is no longer in {lensSet.Name}. Choose a lens."
                : null;

        var noteLeft = Note(lead.SphereLeft, lead.CylinderLeft, lead.AxisLeft, lead.AddLeft, left, form.LensLeftId);
        var noteRight = Note(lead.SphereRight, lead.CylinderRight, lead.AxisRight, lead.AddRight, right, form.SameLensForBothEyes ? form.LensLeftId : form.LensRightId);
        return (noteLeft, noteRight);
    }

    /// <summary>Each lens set reaching the Lead's retail point with its lenses in the Rules' fixed
    /// order (single vision by sphere, then Bifocal, Progressive, Other by add) — one definition of
    /// the order, shared with the Field App.</summary>
    private static IReadOnlyList<LensSetChoice> LensSetChoices(IEnumerable<PresetCatalogueDto> catalogues, ReferenceDataSnapshot atLeadsLocation) =>
        catalogues
            .Select(set => new LensSetChoice(
                set.Id,
                set.Name,
                LensSetLenses.InDisplayOrder(atLeadsLocation.FindCatalogue(set.Id)?.LensOptions ?? [], atLeadsLocation)
                    .Select(lens => new LensChoice(lens.Id, lens.Label, LensOptionCard.FormatLensPower(lens.Sphere, lens.Cylinder, lens.Axis, lens.Add), lens.LensTypeRefId))
                    .ToList()))
            .ToList();

    /// <summary>
    /// Seeds the answers from the Lead and overlays what this form asked — the same shared builder
    /// ConsultationForm.razor uses, so a field added to CreateSaleRequest cannot reach one write
    /// path and miss the other (which is how the referral answers came to be missing from this
    /// one). Carry-over and the conditional blanking live in SaleAssembly; what stays here is only
    /// the part that is genuinely this form's own: which controls it rendered.
    /// </summary>
    private static SaleAnswers BuildSaleAnswers(LeadDto lead, LeadConversionFormModel form, bool leadLensCarriesOver)
    {
        var answers = SaleAssembly.Seed(lead) with
        {
            ConsentGiven = form.ConsentGiven,
            FrameColourRefId = form.FrameColourRefId,
            FrameColourOtherText = form.FrameColourOtherText,
            CoatingRefIds = form.CoatingRefIds,
            HardCaseSold = form.HardCaseSold,
            HardCaseColourRefId = form.HardCaseColourRefId,
            HardCaseOtherColourText = form.HardCaseOtherColourText,
            // Passed through as ticked. This form renders the checkbox unconditionally with its
            // "Custom range only" condition in the label, so ConsultationRules saying so on submit
            // is the intended feedback — see SaleAnswers.OrderFromDotGlasses.
            OrderFromDotGlasses = form.OrderFromDotGlasses,
            ReferredOrTreated = form.ReferredOrTreated,
            ReferralReasonRefId = form.ReferralReasonRefId,
            ReferralOtherText = form.ReferralOtherText,
            TreatedInFacility = form.TreatedInFacility,
            ReferralLocationFreeText = form.ReferralLocationFreeText,
        };

        // The lens block is the Lead's whenever it carries over — Seed has already put it there,
        // and this form showed a read-only summary rather than asking. Otherwise the form rendered
        // the lens controls, and their answers replace whatever Seed carried.
        if (!leadLensCarriesOver)
        {
            answers = answers.WithLens(
                form.LensRangeType, form.PresetCatalogueId,
                form.SphereLeft, form.CylinderLeft, form.AxisLeft, form.AddLeft,
                form.SphereRight, form.CylinderRight, form.AxisRight, form.AddRight,
                form.LensTypeRefId, form.LensTypeOtherText,
                form.PupilDistanceMm, form.PresetPupilDistanceBucket, form.ChildrensFrame);
        }

        return answers;
    }

    private async Task<LeadConversionViewModel> BuildViewModelAsync(LeadDto lead, LeadConversionFormModel form, CancellationToken cancellationToken)
    {
        var referenceData = await referenceDataQueryService.ListActiveAsync(cancellationToken);
        var catalogues = await presetCatalogueQueryService.ListAvailableForCallerAsync(lead.HierarchyPath, cancellationToken);

        // The dropdowns above deliberately stay on the active-only list — an admin must not be
        // able to pick a retired option. The read-only lens summary below is the opposite case:
        // it describes what the Lead already recorded, which may point at an option retired since,
        // so it resolves against the snapshot (retired items included) instead.
        var atLeadsLocation = await SnapshotAtLeadsLocationAsync(lead, cancellationToken);
        var lensCarriesOver = LeadLensCarriesOver(lead, atLeadsLocation);

        // What the chosen pair of lenses offers — the Lead's own when its lens carried over, else
        // the pair the form names. No pair (Custom, or a lens still to choose) lists every coating.
        var coatings = ChosenPair(lead, form, atLeadsLocation, lensCarriesOver) is var (left, right)
            ? LensSetLenses.CoatingsFor(left, right)
            : null;
        var (noteLeft, noteRight) = LensNotes(lead, form, atLeadsLocation);

        return new LeadConversionViewModel
        {
            Lead = lead,
            CustomerFullName = lead.CustomerFullName,
            CustomerPhoneNumber = lead.CustomerPhoneNumber,
            LensCarriedOver = lensCarriesOver,
            UnavailableLensSetName = lead.LensRangeType is LensRangeType.LensSet && !atLeadsLocation.IsLensSetAvailable(lead.PresetCatalogueId)
                ? atLeadsLocation.FindCatalogue(lead.PresetCatalogueId)?.Name ?? ReferenceDataSnapshot.MissingLabel
                : null,
            LensNoLongerInSetName = LensNoLongerInSet(lead, atLeadsLocation)
                ? atLeadsLocation.FindCatalogue(lead.PresetCatalogueId)?.Name
                : null,
            LensSummary = BuildLensSummary(lead, atLeadsLocation),
            LensSets = LensSetChoices(catalogues, atLeadsLocation),
            LensNoteLeft = noteLeft,
            LensNoteRight = noteRight,
            OfferedCoatingIds = coatings?.Offered,
            CoatingPairings = coatings?.RequiredPairings ?? [],
            CoatingsNote = coatings is { Offered.Count: 0 }
                ? "No coating can be made on both of these lenses, so they can't be sold together on a lens set — choose another lens."
                : null,
            FrameColours = referenceData.Where(x => x.Category == ReferenceDataCategory.FrameColour).OrderBy(x => x.SortOrder).ToList(),
            Coatings = referenceData.Where(x => x.Category == ReferenceDataCategory.Coating).OrderBy(x => x.SortOrder).ToList(),
            HardCaseColours = referenceData.Where(x => x.Category == ReferenceDataCategory.HardCaseColour).OrderBy(x => x.SortOrder).ToList(),
            ReferralReasons = referenceData.Where(x => x.Category == ReferenceDataCategory.ReferralReason).OrderBy(x => x.SortOrder).ToList(),
            LensTypes = referenceData.Where(x => x.Category == ReferenceDataCategory.LensType).OrderBy(x => x.SortOrder).ToList(),
            Form = form,
        };
    }

    private static string? BuildLensSummary(LeadDto lead, ReferenceDataSnapshot referenceData)
    {
        switch (lead.LensRangeType)
        {
            case LensRangeType.LensSet:
                // Only shown when the lens carries over, which needs both lenses matched; the
                // fallback is for completeness, not a state this screen renders.
                var catalogue = referenceData.FindCatalogue(lead.PresetCatalogueId);
                var (left, right) = LeadsLenses(lead, referenceData);
                return $"{catalogue?.Name ?? "Lens set"} — Left: {left?.Label ?? ReferenceDataSnapshot.MissingLabel}, Right: {right?.Label ?? ReferenceDataSnapshot.MissingLabel}";
            case LensRangeType.Custom:
                // Still keyed off the id being present, not off the item resolving: a Lead with no
                // lens type recorded omits the clause entirely, exactly as before. What changes is
                // that a lens type retired since the Lead was captured now renders its label
                // instead of silently dropping the clause.
                var lensTypeSummary = lead.LensTypeRefId is null
                    ? null
                    : $"; Lens type {referenceData.ResolveLabel(lead.LensTypeRefId, lead.LensTypeOtherText)}";
                return $"Custom — OD (right) Sphere {lead.SphereRight} / Cyl {lead.CylinderRight} / Axis {lead.AxisRight} / Add {lead.AddRight}; "
                    + $"OS (left) Sphere {lead.SphereLeft} / Cyl {lead.CylinderLeft} / Axis {lead.AxisLeft} / Add {lead.AddLeft}; "
                    + $"PD {(lead.PupilDistanceMm is { } pd ? $"{pd}mm" : "not recorded")}{lensTypeSummary}";
            default:
                return null;
        }
    }
}

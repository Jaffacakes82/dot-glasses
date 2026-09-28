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
        var form = new LeadConversionFormModel { ConsentGiven = seeded.ConsentGiven, CoatingRefIds = seeded.CoatingRefIds };

        // A Lead whose lens set still reaches it but no longer holds one of its lenses asks again —
        // with the set, and whichever lens still matches, pre-selected rather than blank.
        var atLeadsLocation = await SnapshotAtLeadsLocationAsync(lead, cancellationToken);
        if (LensNoLongerInSet(lead, atLeadsLocation))
        {
            var (left, right) = LeadsLenses(lead, atLeadsLocation);
            form.LensRange = LensRangeChoice.Format(LensRangeType.LensSet, lead.PresetCatalogueId);
            form.LensLeftId = left?.Id;
            form.LensRightId = right?.Id;
            form.PresetPupilDistanceBucket = lead.PresetPupilDistanceBucket;
            form.ChildrensFrame = lead.ChildrensFrame;
        }

        return View(await BuildViewModelAsync(lead, form, cancellationToken));
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
        var retailPoint = await organisationNodeLookup.FindByHierarchyPathAsync(lead.HierarchyPath, cancellationToken);
        if (retailPoint is not { IsActive: true })
        {
            throw new DomainRuleViolationException($"{retailPoint?.Name ?? "This lead's retail point"} has been deactivated.");
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
            AvailableCatalogues = catalogues,
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

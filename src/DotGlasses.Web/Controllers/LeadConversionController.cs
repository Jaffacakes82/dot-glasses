using DotGlasses.Application.Leads;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Sales;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.ReferenceData;
using DotGlasses.Contracts.Sales;
using DotGlasses.Rules;
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
    IPresetCatalogueQueryService presetCatalogueQueryService) : Controller
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

        var request = BuildCreateSaleRequest(lead, form);

        // The same module SalesController checks against, off the same per-request snapshot
        // BuildViewModelAsync already loads. The source-Lead check the two API endpoints also run
        // has nothing to do here: it asks whether this Lead is already converted, and the
        // ConvertedFlag guard above has answered that with friendlier copy — SaleService sets
        // ConvertedFlag and SaleId together in one transaction, so the two can't disagree.
        var snapshot = await referenceDataSnapshotProvider.GetAsync(cancellationToken);
        var rules = ConsultationRules.Check(request, snapshot);
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

    /// <summary>
    /// Seeds the answers from the Lead, overlays what this form asked, and hands both to the shared
    /// builder — the same one ConsultationForm.razor uses, so a field added to CreateSaleRequest
    /// cannot reach one write path and miss the other (which is how the referral answers came to be
    /// missing from this one). Carry-over and the conditional blanking live in SaleAssembly; what
    /// stays here is only the part that is genuinely this form's own: which controls it rendered.
    /// </summary>
    private static CreateSaleRequest BuildCreateSaleRequest(LeadDto lead, LeadConversionFormModel form)
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

        // The lens block is the Lead's whenever it recorded one — Seed has already carried it over,
        // and this form showed a read-only summary rather than asking. Only when it recorded none
        // does the form render the lens controls, and only then do its answers apply.
        if (!SaleAssembly.CarriesLens(lead))
        {
            answers = answers.WithLens(
                form.LensRangeType, form.PresetCatalogueId, form.LensOptionLeftId, form.LensOptionRightId,
                form.CustomSphereLeft, form.CustomCylinderLeft, form.CustomAxisLeft, form.CustomAddPowerLeft,
                form.CustomSphereRight, form.CustomCylinderRight, form.CustomAxisRight, form.CustomAddPowerRight,
                form.LensTypeRefId, form.LensTypeOtherText,
                form.PupilDistanceMm, form.PresetPupilDistanceBucket, form.ChildrensFrame);
        }

        return SaleAssembly.Build(Guid.NewGuid(), lead.Id, answers);
    }

    private async Task<LeadConversionViewModel> BuildViewModelAsync(LeadDto lead, LeadConversionFormModel form, CancellationToken cancellationToken)
    {
        var referenceData = await referenceDataQueryService.ListActiveAsync(cancellationToken);
        var catalogues = await presetCatalogueQueryService.ListAvailableForCallerAsync(lead.HierarchyPath, cancellationToken);

        // The dropdowns above deliberately stay on the active-only list — an admin must not be
        // able to pick a retired option. The read-only lens summary below is the opposite case:
        // it describes what the Lead already recorded, which may point at an option retired since,
        // so it resolves against the snapshot (retired items included) instead.
        var referenceDataSnapshot = await referenceDataSnapshotProvider.GetAsync(cancellationToken);

        return new LeadConversionViewModel
        {
            Lead = lead,
            CustomerFullName = lead.CustomerFullName,
            CustomerPhoneNumber = lead.CustomerPhoneNumber,
            LensCarriedOver = SaleAssembly.CarriesLens(lead),
            LensSummary = BuildLensSummary(lead, referenceDataSnapshot),
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
            case LensRangeType.SixLensSet or LensRangeType.NineLensSet:
                var catalogue = referenceData.FindCatalogue(lead.PresetCatalogueId);
                var left = referenceData.ResolveLensOptionLabel(lead.LensOptionLeftId);
                var right = referenceData.ResolveLensOptionLabel(lead.LensOptionRightId);
                return $"{catalogue?.Name ?? "Preset range"} — Left: {left}, Right: {right}";
            case LensRangeType.Custom:
                // Still keyed off the id being present, not off the item resolving: a Lead with no
                // lens type recorded omits the clause entirely, exactly as before. What changes is
                // that a lens type retired since the Lead was captured now renders its label
                // instead of silently dropping the clause.
                var lensTypeSummary = lead.LensTypeRefId is null
                    ? null
                    : $"; Lens type {referenceData.ResolveLabel(lead.LensTypeRefId, lead.LensTypeOtherText)}";
                return $"Custom — OD (right) Sphere {lead.CustomSphereRight} / Cyl {lead.CustomCylinderRight} / Axis {lead.CustomAxisRight} / Add {lead.CustomAddPowerRight}; "
                    + $"OS (left) Sphere {lead.CustomSphereLeft} / Cyl {lead.CustomCylinderLeft} / Axis {lead.CustomAxisLeft} / Add {lead.CustomAddPowerLeft}; "
                    + $"PD {(lead.PupilDistanceMm is { } pd ? $"{pd}mm" : "not recorded")}{lensTypeSummary}";
            default:
                return null;
        }
    }
}

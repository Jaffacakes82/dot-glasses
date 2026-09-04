using DotGlasses.Application.Leads;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Sales;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.ReferenceData;
using DotGlasses.Contracts.Sales;
using DotGlasses.Web.Models;
using FluentValidation;
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
    IPresetCatalogueQueryService presetCatalogueQueryService,
    IValidator<CreateSaleRequest> validator) : Controller
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

        var form = new LeadConversionFormModel { ConsentGiven = lead.ConsentGiven, CoatingRefId = lead.CoatingPreferenceRefId };
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

        var lensCarriedOver = lead.LensRangeType is not null;
        var request = BuildCreateSaleRequest(lead, form, lensCarriedOver);

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            // Errors come back keyed by CreateSaleRequest's own property names — LeadConversionFormModel
            // deliberately mirrors those names 1:1 so a straight "Form.{PropertyName}" remap is enough,
            // no per-field translation table needed.
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError($"{nameof(form)}.{error.PropertyName}", error.ErrorMessage);
            }

            return View(await BuildViewModelAsync(lead, form, cancellationToken));
        }

        await saleService.CreateAsync(request, lead.TechnicianUserId, lead.HierarchyPath, cancellationToken);

        TempData["Info"] = "Lead converted into a sale.";
        return RedirectToAction("Index", "EventHistory", new { tab = "leads" });
    }

    private static CreateSaleRequest BuildCreateSaleRequest(LeadDto lead, LeadConversionFormModel form, bool lensCarriedOver) => new()
    {
        Id = Guid.NewGuid(),
        SourceLeadId = lead.Id,
        FullName = lead.CustomerFullName,
        PhoneNumber = lead.CustomerPhoneNumber,
        AgeYears = lead.AgeYears,
        Gender = lead.Gender,
        OccupationRefId = lead.OccupationRefId,
        OccupationOtherText = lead.OccupationOtherText,
        ConsentGiven = form.ConsentGiven,
        LensRangeType = lensCarriedOver ? lead.LensRangeType!.Value : form.LensRangeType ?? LensRangeType.Custom,
        PresetCatalogueId = lensCarriedOver ? lead.PresetCatalogueId : form.PresetCatalogueId,
        LensOptionLeftId = lensCarriedOver ? lead.LensOptionLeftId : form.LensOptionLeftId,
        LensOptionRightId = lensCarriedOver ? lead.LensOptionRightId : form.LensOptionRightId,
        CustomSphereLeft = lensCarriedOver ? lead.CustomSphereLeft : form.CustomSphereLeft,
        CustomCylinderLeft = lensCarriedOver ? lead.CustomCylinderLeft : form.CustomCylinderLeft,
        CustomAxisLeft = lensCarriedOver ? lead.CustomAxisLeft : form.CustomAxisLeft,
        CustomAddPowerLeft = lensCarriedOver ? lead.CustomAddPowerLeft : form.CustomAddPowerLeft,
        CustomSphereRight = lensCarriedOver ? lead.CustomSphereRight : form.CustomSphereRight,
        CustomCylinderRight = lensCarriedOver ? lead.CustomCylinderRight : form.CustomCylinderRight,
        CustomAxisRight = lensCarriedOver ? lead.CustomAxisRight : form.CustomAxisRight,
        CustomAddPowerRight = lensCarriedOver ? lead.CustomAddPowerRight : form.CustomAddPowerRight,
        LensTypeRefId = lensCarriedOver ? lead.LensTypeRefId : form.LensTypeRefId,
        LensTypeOtherText = lensCarriedOver ? lead.LensTypeOtherText : form.LensTypeOtherText,
        OrderFromDotGlasses = form.OrderFromDotGlasses,
        PupilDistanceMm = lensCarriedOver ? lead.PupilDistanceMm : form.PupilDistanceMm,
        PresetPupilDistanceBucket = lensCarriedOver ? lead.PresetPupilDistanceBucket : form.PresetPupilDistanceBucket,
        ChildrensFrame = lensCarriedOver ? lead.ChildrensFrame : form.ChildrensFrame,
        FrameColourRefId = form.FrameColourRefId ?? Guid.Empty,
        FrameColourOtherText = form.FrameColourOtherText,
        FrameCoverage = form.FrameCoverage,
        CoatingRefId = form.CoatingRefId,
        HardCaseSold = form.HardCaseSold,
        HardCaseColourRefId = form.HardCaseSold ? form.HardCaseColourRefId : null,
        HardCaseOtherColourText = form.HardCaseSold ? form.HardCaseOtherColourText : null,
    };

    private async Task<LeadConversionViewModel> BuildViewModelAsync(LeadDto lead, LeadConversionFormModel form, CancellationToken cancellationToken)
    {
        var referenceData = await referenceDataQueryService.ListActiveAsync(cancellationToken);
        var catalogues = await presetCatalogueQueryService.ListAvailableForCallerAsync(lead.HierarchyPath, cancellationToken);

        return new LeadConversionViewModel
        {
            Lead = lead,
            CustomerFullName = lead.CustomerFullName,
            CustomerPhoneNumber = lead.CustomerPhoneNumber,
            LensCarriedOver = lead.LensRangeType is not null,
            LensSummary = BuildLensSummary(lead, catalogues, referenceData),
            AvailableCatalogues = catalogues,
            FrameColours = referenceData.Where(x => x.Category == ReferenceDataCategory.FrameColour).OrderBy(x => x.SortOrder).ToList(),
            Coatings = referenceData.Where(x => x.Category == ReferenceDataCategory.Coating).OrderBy(x => x.SortOrder).ToList(),
            HardCaseColours = referenceData.Where(x => x.Category == ReferenceDataCategory.HardCaseColour).OrderBy(x => x.SortOrder).ToList(),
            LensTypes = referenceData.Where(x => x.Category == ReferenceDataCategory.LensType).OrderBy(x => x.SortOrder).ToList(),
            Form = form,
        };
    }

    private static string? BuildLensSummary(LeadDto lead, IReadOnlyList<PresetCatalogueDto> catalogues, IReadOnlyList<ReferenceDataItemDto> referenceData)
    {
        switch (lead.LensRangeType)
        {
            case LensRangeType.SixLensSet or LensRangeType.NineLensSet:
                var catalogue = catalogues.FirstOrDefault(c => c.Id == lead.PresetCatalogueId);
                var left = catalogue?.LensOptions.FirstOrDefault(o => o.Id == lead.LensOptionLeftId)?.Label ?? "—";
                var right = catalogue?.LensOptions.FirstOrDefault(o => o.Id == lead.LensOptionRightId)?.Label ?? "—";
                return $"{catalogue?.Name ?? "Preset range"} — Left: {left}, Right: {right}";
            case LensRangeType.Custom:
                var lensType = referenceData.FirstOrDefault(x => x.Id == lead.LensTypeRefId);
                var lensTypeSummary = lensType is null ? null : $"; Lens type {(lensType.IsOtherOption ? lead.LensTypeOtherText : lensType.Label)}";
                return $"Custom — OD (right) Sphere {lead.CustomSphereRight} / Cyl {lead.CustomCylinderRight} / Axis {lead.CustomAxisRight} / Add {lead.CustomAddPowerRight}; "
                    + $"OS (left) Sphere {lead.CustomSphereLeft} / Cyl {lead.CustomCylinderLeft} / Axis {lead.CustomAxisLeft} / Add {lead.CustomAddPowerLeft}; "
                    + $"PD {(lead.PupilDistanceMm is { } pd ? $"{pd}mm" : "not recorded")}{lensTypeSummary}";
            default:
                return null;
        }
    }
}

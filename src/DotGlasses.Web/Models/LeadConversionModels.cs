using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;
using DotGlasses.Rules.LensRanges;
using DotGlasses.Rules.LensSets;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Web.Models;

/// <summary>
/// Posted fields for converting a Lead into a Sale — named to match CreateSaleRequest's own
/// property names 1:1 (including the lens-range fields, only rendered/used when the source Lead
/// captured no product preference at all) so FluentValidation errors can be remapped onto
/// "Form.{PropertyName}" ModelState keys without a translation table — see
/// LeadConversionController.Convert(POST).
/// </summary>
public class LeadConversionFormModel
{
    public bool ConsentGiven { get; set; }

    /// <summary>At least one required — see CreateSaleRequest.CoatingRefIds/ADR-0001. No live
    /// pairing-auto-add or exclusion-blocking in this admin form (Day-2 nicety for this
    /// occasional manual-entry path); the exclusion rule is still enforced server-side via the
    /// same ConsultationRules module the Field App posts through, surfaced as a validation
    /// error on submit like every other field here.</summary>
    public List<Guid> CoatingRefIds { get; set; } = [];
    public Guid? FrameColourRefId { get; set; }
    public string? FrameColourOtherText { get; set; }
    public bool HardCaseSold { get; set; }
    public Guid? HardCaseColourRefId { get; set; }
    public string? HardCaseOtherColourText { get; set; }
    public bool OrderFromDotGlasses { get; set; }

    /// <summary>"Referred or treated" — asked here exactly as it is on every other capture path
    /// (Test/Lead/Sale are separate create-once events, so nothing carries forward from the source
    /// Lead's own answer and the admin answers fresh). ReferralReasonRefId is required whenever
    /// ReferredOrTreated is true; ReferralOtherText only when that reason is the "Other" option;
    /// ReferralLocationFreeText is required when TreatedInFacility is false and must be empty when
    /// it's true. The other four must stay empty when ReferredOrTreated is false — see
    /// ConsultationRules' Referral rule, which is what actually enforces this.</summary>
    public bool ReferredOrTreated { get; set; }
    public Guid? ReferralReasonRefId { get; set; }
    public string? ReferralOtherText { get; set; }
    public bool TreatedInFacility { get; set; }
    public string? ReferralLocationFreeText { get; set; }

    // Only rendered/used when the Lead's own lens preference can't carry over — it recorded none,
    // or its lens set no longer reaches the Lead's retail point — otherwise it carries unchanged.
    //
    // LensRange is the one control the admin actually uses: a lens set's id, or "custom"
    // (LensRangeChoice, ADR-0005). ApplyLensRange turns it into the two CreateSaleRequest fields it
    // stands for, which keep their names so rule failures still remap onto "Form.{PropertyName}".
    //
    // LensLeftId/LensRightId are the other exception: which lens set lens each dropdown shows. A
    // Sale records no lens id (ADR-0007), so ApplyLensRange turns them into what it does record —
    // each eye's power and the pair's lens type (LensSetLenses.RecordedAs) — and a rule failure
    // about a lens comes back keyed on SphereLeft/SphereRight.
    public string? LensRange { get; set; }

    public LensRangeType? LensRangeType { get; set; }
    public Guid? PresetCatalogueId { get; set; }
    public Guid? LensLeftId { get; set; }
    public Guid? LensRightId { get; set; }
    public int? PresetPupilDistanceBucket { get; set; }
    public bool ChildrensFrame { get; set; }
    public decimal? SphereLeft { get; set; }
    public decimal? CylinderLeft { get; set; }
    public decimal? AxisLeft { get; set; }
    public decimal? AddLeft { get; set; }
    public decimal? SphereRight { get; set; }
    public decimal? CylinderRight { get; set; }
    public decimal? AxisRight { get; set; }
    public decimal? AddRight { get; set; }
    public Guid? LensTypeRefId { get; set; }
    public string? LensTypeOtherText { get; set; }
    public decimal? PupilDistanceMm { get; set; }

    /// <summary>
    /// Turns the controls into the request's lens fields. On a lens set the chosen lenses replace
    /// every per-eye power field and the lens type: the Custom prescription selects below the lens
    /// dropdowns post too (this form shows both, with no client-side show/hide), and on a lens set
    /// what is recorded is the chosen lenses' own powers, never what those selects happened to hold.
    /// A lens id that isn't in the chosen set records nothing for that eye — the rules then ask for
    /// a lens.
    /// </summary>
    public void ApplyLensRange(ReferenceDataSnapshot referenceData)
    {
        (LensRangeType, PresetCatalogueId) = LensRangeChoice.Parse(LensRange);
        if (LensRangeType is not Contracts.Common.LensRangeType.LensSet)
        {
            return;
        }

        var lenses = referenceData.FindCatalogue(PresetCatalogueId)?.LensOptions ?? [];
        var recorded = LensSetLenses.RecordedAs(
            lenses.FirstOrDefault(lens => lens.Id == LensLeftId),
            lenses.FirstOrDefault(lens => lens.Id == LensRightId));

        (SphereLeft, CylinderLeft, AxisLeft, AddLeft) = (recorded.SphereLeft, recorded.CylinderLeft, recorded.AxisLeft, recorded.AddLeft);
        (SphereRight, CylinderRight, AxisRight, AddRight) = (recorded.SphereRight, recorded.CylinderRight, recorded.AxisRight, recorded.AddRight);
        (LensTypeRefId, LensTypeOtherText) = (recorded.LensTypeRefId, recorded.LensTypeOtherText);
    }
}

/// <summary>LensCarriedOver is true when the Lead already captured a product preference — in
/// that case the lens/prescription section of the form is a read-only summary (LensSummary) and
/// the admin only supplies the genuinely-new Sale fields (frame, coating, hard case, order).
/// When false, the admin must also pick a lens range — see LeadConversionFormModel.
/// UnavailableLensSetName is set when the Lead did record a lens set but it no longer reaches the
/// Lead's retail point (retired or unassigned since): the screen says so and asks afresh rather
/// than leaving the admin with a summary they can't act on. LensNoLongerInSetName is the same
/// for a Lead whose lens set still reaches it but no longer holds one of its lenses (matched by
/// power and lens type, LensSetLenses.Match): the set and whichever lens still matches are
/// pre-selected, and the admin chooses the rest.</summary>
public class LeadConversionViewModel
{
    public required LeadDto Lead { get; init; }
    public required string CustomerFullName { get; init; }
    public required string? CustomerPhoneNumber { get; init; }
    public required bool LensCarriedOver { get; init; }
    public required string? UnavailableLensSetName { get; init; }
    public string? LensNoLongerInSetName { get; init; }
    public required string? LensSummary { get; init; }
    public required IReadOnlyList<PresetCatalogueDto> AvailableCatalogues { get; init; }
    public required IReadOnlyList<ReferenceDataItemDto> FrameColours { get; init; }
    public required IReadOnlyList<ReferenceDataItemDto> Coatings { get; init; }
    public required IReadOnlyList<ReferenceDataItemDto> HardCaseColours { get; init; }
    public required IReadOnlyList<ReferenceDataItemDto> ReferralReasons { get; init; }
    public required IReadOnlyList<ReferenceDataItemDto> LensTypes { get; init; }
    public required LeadConversionFormModel Form { get; init; }
}

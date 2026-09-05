using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Sales;

namespace DotGlasses.Rules.Sales;

/// <summary>
/// Everything a Sale is assembled from, as the two write paths actually hold it: the answers a
/// human supplied, before the conditional blanking and absence encodings
/// <see cref="SaleAssembly.Build"/> applies on the way to <see cref="CreateSaleRequest"/>.
///
/// Property names match <see cref="CreateSaleRequest"/>'s 1:1 — the same convention
/// LeadConversionFormModel already follows, and for the same reason: it makes the mapping
/// inspectable rather than a translation table, and it is what lets SaleAssemblyTests check by
/// reflection that a field added to the request cannot quietly miss the builder.
///
/// Two differences from the request are deliberate, and both are about representing "not
/// supplied" honestly rather than inventing a value:
/// <list type="bullet">
/// <item><see cref="LensRangeType"/> is nullable here — a half-filled form has not chosen one, and
/// <see cref="CreateSaleRequest.LensRangeType"/> has no way to say so.</item>
/// <item><see cref="FrameColourRefId"/> is nullable here for the same reason.</item>
/// </list>
/// <see cref="SaleAssembly.Build"/> is the single place either becomes the request's non-nullable
/// field, and its comments say what rejects the absent case.
///
/// <b>Not here on purpose:</b> the Id and the source-Lead link are arguments to
/// <see cref="SaleAssembly.Build"/>, not answers — see that method. Nor is attribution
/// (TechnicianUserId/HierarchyPath): it is not on the request DTO at all, and must not be added
/// (CLAUDE.md — create requests never accept it from the client; a converted Sale is attributed to
/// the Lead's own technician and retail point by ISaleService.CreateAsync's separate arguments).
/// </summary>
public sealed record SaleAnswers
{
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }

    public int? AgeYears { get; init; }
    public Gender Gender { get; init; }
    public Guid? OccupationRefId { get; init; }
    public string? OccupationOtherText { get; init; }

    public bool ConsentGiven { get; init; }

    /// <summary>"Referred or treated", as answered — <see cref="SaleAssembly.Build"/> applies the
    /// suppression, so a form may pass what its controls currently hold without pre-blanking.</summary>
    public bool ReferredOrTreated { get; init; }
    public Guid? ReferralReasonRefId { get; init; }
    public string? ReferralOtherText { get; init; }
    public string? ReferralLocationFreeText { get; init; }
    public bool TreatedInFacility { get; init; }

    /// <summary>Null until a range is chosen — see the class summary.</summary>
    public LensRangeType? LensRangeType { get; init; }

    public Guid? PresetCatalogueId { get; init; }
    public Guid? LensOptionLeftId { get; init; }
    public Guid? LensOptionRightId { get; init; }

    public decimal? CustomSphereLeft { get; init; }
    public decimal? CustomCylinderLeft { get; init; }
    public decimal? CustomAxisLeft { get; init; }
    public decimal? CustomAddPowerLeft { get; init; }
    public decimal? CustomSphereRight { get; init; }
    public decimal? CustomCylinderRight { get; init; }
    public decimal? CustomAxisRight { get; init; }
    public decimal? CustomAddPowerRight { get; init; }

    public Guid? LensTypeRefId { get; init; }
    public string? LensTypeOtherText { get; init; }

    /// <summary>
    /// Passed through as supplied. The "Custom range only" condition is deliberately <i>not</i>
    /// applied here, because the two forms need opposite things from it: the Field App hides this
    /// checkbox entirely outside a Custom range, so a value left over from an earlier Custom
    /// selection has to be suppressed where it is gathered or it becomes an error against a
    /// control the technician cannot see; the Admin Portal renders it unconditionally with the
    /// condition spelled out in its label and relies on ConsultationRules to say so on submit.
    /// Suppressing it here would silently swallow the admin's mistake — so it stays each form's
    /// own business.
    /// </summary>
    public bool OrderFromDotGlasses { get; init; }

    public decimal? PupilDistanceMm { get; init; }
    public int? PresetPupilDistanceBucket { get; init; }

    public bool ChildrensFrame { get; init; }

    /// <summary>Null until a colour is chosen — see the class summary.</summary>
    public Guid? FrameColourRefId { get; init; }
    public string? FrameColourOtherText { get; init; }

    /// <summary>Not asked on either write path (2026-09-04) — neither form renders a control, so
    /// both record the FullFrame default. The field stays so existing records read back
    /// unchanged.</summary>
    public FrameCoverage FrameCoverage { get; init; } = FrameCoverage.FullFrame;

    /// <summary>The <b>Coating set</b> (CONTEXT.md) — a set, not a single value. Seeded from a
    /// converted Lead's single <b>Coating preference</b> by <see cref="SaleAssembly.Seed"/>.</summary>
    public List<Guid> CoatingRefIds { get; init; } = [];

    /// <summary>As answered — <see cref="SaleAssembly.Build"/> blanks the two colour fields when
    /// no case was sold, so a form may pass what its controls currently hold.</summary>
    public bool HardCaseSold { get; init; }
    public Guid? HardCaseColourRefId { get; init; }
    public string? HardCaseOtherColourText { get; init; }

    /// <summary>
    /// Replaces the lens block as a unit. It is one decision — which lenses this Sale is for — and
    /// replacing it field by field at each call site is how the two write paths drifted in the
    /// first place, so the field list is written here once and both callers go through it:
    /// <see cref="SaleAssembly.Seed"/> to carry a Lead's own recorded lenses over, and the Admin
    /// Portal to supply the admin's answers when the Lead recorded none
    /// (<see cref="SaleAssembly.CarriesLens"/>).
    /// </summary>
    public SaleAnswers WithLens(
        LensRangeType? lensRangeType, Guid? presetCatalogueId, Guid? lensOptionLeftId, Guid? lensOptionRightId,
        decimal? customSphereLeft, decimal? customCylinderLeft, decimal? customAxisLeft, decimal? customAddPowerLeft,
        decimal? customSphereRight, decimal? customCylinderRight, decimal? customAxisRight, decimal? customAddPowerRight,
        Guid? lensTypeRefId, string? lensTypeOtherText,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool childrensFrame) =>
        this with
        {
            LensRangeType = lensRangeType,
            PresetCatalogueId = presetCatalogueId,
            LensOptionLeftId = lensOptionLeftId,
            LensOptionRightId = lensOptionRightId,
            CustomSphereLeft = customSphereLeft,
            CustomCylinderLeft = customCylinderLeft,
            CustomAxisLeft = customAxisLeft,
            CustomAddPowerLeft = customAddPowerLeft,
            CustomSphereRight = customSphereRight,
            CustomCylinderRight = customCylinderRight,
            CustomAxisRight = customAxisRight,
            CustomAddPowerRight = customAddPowerRight,
            LensTypeRefId = lensTypeRefId,
            LensTypeOtherText = lensTypeOtherText,
            PupilDistanceMm = pupilDistanceMm,
            PresetPupilDistanceBucket = presetPupilDistanceBucket,
            ChildrensFrame = childrensFrame,
        };
}

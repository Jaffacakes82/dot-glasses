using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;

namespace DotGlasses.Rules.Sales;

/// <summary>
/// How a Sale creation request is put together, in one place for both write paths — the Field App's
/// consultation form and the Admin Portal's Lead→Sale conversion screen. Both used to assemble
/// <see cref="CreateSaleRequest"/> by hand, field by field, and the copies drifted: the referral
/// answers reached the request on the Field App's path and were dropped on the admin's, which is
/// what ticket 01 point-fixed and this replaces.
///
/// It lives in <c>DotGlasses.Rules</c> for the reason ADR-0002 gives for the rule module beside it:
/// it is shared by <c>App</c> and <c>Web</c>, and <c>Rules</c> is the only project <c>App</c> may
/// reference besides <c>Contracts</c> — which is where both <see cref="LeadDto"/> and
/// <see cref="CreateSaleRequest"/> already live, so nothing new is dragged onto the device. Like
/// the rules beside it this is a pure function over DTOs: no I/O, and no reference-data snapshot
/// either, because assembling a request asks nothing of the reference-data library — whether the
/// ids it carries are real is <see cref="ConsultationRules.Check(CreateSaleRequest, ReferenceData.ReferenceDataSnapshot)"/>'s
/// question, asked next, off the same request this produces.
///
/// The split into <see cref="Seed"/> and <see cref="Build"/> is not decoration — it is what lets
/// both paths share the rules without either being forced into the other's shape. See
/// <see cref="Seed"/>.
/// </summary>
public static class SaleAssembly
{
    /// <summary>
    /// Whether a Lead already recorded which lenses it was for. When it did, the converting form
    /// must not ask again — the Admin Portal shows a read-only summary instead, and
    /// <see cref="Seed"/> carries the Lead's own values over. When it did not, the converting form
    /// asks, and supplies the answers through <see cref="SaleAnswers.WithLens"/>.
    /// </summary>
    public static bool CarriesLens(LeadDto lead) => lead.LensRangeType is not null;

    /// <summary>
    /// What a Lead contributes to the Sale that converts it — the carry-over rule, written once.
    ///
    /// Three groups, and the boundaries are the point. <b>Carried:</b> the customer's identity and
    /// demographics, the consent already given, and — when <see cref="CarriesLens"/> — the whole
    /// lens block. The Sale's <b>Coating set</b> is seeded from the Lead's single <b>Coating
    /// preference</b>, which is the one place those two different concepts meet (CONTEXT.md); a
    /// Lead with no preference seeds an empty set, and the technician picks one. <b>Not carried:</b>
    /// frame colour, hard case, and the coating decisions beyond that seed — genuinely new choices
    /// made at the point of sale, which no Lead could have recorded. <b>Also not carried:</b> the
    /// "referred or treated" answers. Test/Lead/Sale are separate create-once events and each asks
    /// fresh; carrying the Lead's answer forward would record a referral that never happened at
    /// this visit.
    ///
    /// This is a <i>seed</i>, not an override, and that distinction is load-bearing. The result is
    /// the starting point for the answers a human then supplies — so the Admin Portal seeds at
    /// build time (its form holds the Lead and the answers as two separate objects), while the
    /// Field App seeds at <i>load</i> time, into the form controls the technician can then edit.
    /// Applying the Lead over the answers at build time instead would break both: it would discard
    /// the technician's edits, and on the Field App's automatic conversion-match path — where a
    /// matching open Lead is found only <i>after</i> the whole form is filled in — it would replace
    /// everything just typed with the older Lead's values.
    /// </summary>
    public static SaleAnswers Seed(LeadDto lead)
    {
        var seeded = new SaleAnswers
        {
            FullName = lead.CustomerFullName,
            PhoneNumber = lead.CustomerPhoneNumber,
            AgeYears = lead.AgeYears,
            Gender = lead.Gender,
            OccupationRefId = lead.OccupationRefId,
            OccupationOtherText = lead.OccupationOtherText,
            ConsentGiven = lead.ConsentGiven,
            CoatingRefIds = lead.CoatingPreferenceRefId is { } coatingRefId ? [coatingRefId] : [],
        };

        return CarriesLens(lead)
            ? seeded.WithLens(
                lead.LensRangeType, lead.PresetCatalogueId, lead.LensOptionLeftId, lead.LensOptionRightId,
                lead.CustomSphereLeft, lead.CustomCylinderLeft, lead.CustomAxisLeft, lead.CustomAddPowerLeft,
                lead.CustomSphereRight, lead.CustomCylinderRight, lead.CustomAxisRight, lead.CustomAddPowerRight,
                lead.LensTypeRefId, lead.LensTypeOtherText,
                lead.PupilDistanceMm, lead.PresetPupilDistanceBucket, lead.ChildrensFrame)
            : seeded;
    }

    /// <summary>
    /// Assembles the request both paths send. Everything on <see cref="CreateSaleRequest"/> comes
    /// from <paramref name="answers"/> except the two identifiers, which are not answers:
    /// <paramref name="id"/> is the offline-sync outbox's idempotency key (a fresh Guid, or a
    /// corrected record's own so the server's upsert still applies), and
    /// <paramref name="sourceLeadId"/> is the link to the Lead being converted — null for a Sale
    /// made from scratch, and on the Field App's conversion-match path only decided at submit time,
    /// after the answers were gathered.
    ///
    /// <b>Attribution is not here and must not be added.</b> TechnicianUserId/HierarchyPath are
    /// deliberately absent from the request DTO (CLAUDE.md) — the server stamps them, and for a
    /// converted Sale LeadConversionController passes the <i>Lead's</i> own values to
    /// ISaleService.CreateAsync as separate arguments, so the Sale is attributed to the outlet
    /// where it happened rather than to the converting admin.
    ///
    /// What this method does beyond copying is suppress answers that their own condition has since
    /// turned off — a box ticked, detail filled in, then unticked. Both forms already did this,
    /// identically and by hand, for the same stated reason: without it they submit a request that
    /// can only fail, against a control that in the Field App's case is no longer even rendered.
    /// The gate on OrderFromDotGlasses is the one exception, and stays with each form — see
    /// <see cref="SaleAnswers.OrderFromDotGlasses"/>.
    /// </summary>
    public static CreateSaleRequest Build(Guid id, Guid? sourceLeadId, SaleAnswers answers) => new()
    {
        Id = id,
        SourceLeadId = sourceLeadId,

        FullName = answers.FullName,
        PhoneNumber = string.IsNullOrWhiteSpace(answers.PhoneNumber) ? null : answers.PhoneNumber,
        AgeYears = answers.AgeYears,
        Gender = answers.Gender,
        OccupationRefId = answers.OccupationRefId,
        OccupationOtherText = answers.OccupationOtherText,
        ConsentGiven = answers.ConsentGiven,

        // The four detail fields are only meaningful when ReferredOrTreated is true, and the rule
        // module rejects the request outright if any is non-empty when it is false. TreatedInFacility
        // suppresses ReferralLocationFreeText the same way — the rules require that empty when the
        // treatment happened in the facility.
        ReferredOrTreated = answers.ReferredOrTreated,
        ReferralReasonRefId = answers.ReferredOrTreated ? answers.ReferralReasonRefId : null,
        ReferralOtherText = answers.ReferredOrTreated ? answers.ReferralOtherText : null,
        TreatedInFacility = answers.ReferredOrTreated && answers.TreatedInFacility,
        ReferralLocationFreeText = answers.ReferredOrTreated && !answers.TreatedInFacility ? answers.ReferralLocationFreeText : null,

        // No range chosen yet has no representation on the request — the field is non-nullable, so
        // the request cannot say "unanswered" and Custom is what an unanswered form has always
        // sent. It does not pass silently: Custom then requires a prescription and a pupil
        // distance, none of which an unanswered form carries, so ConsultationRules rejects it.
        // The messages name the missing prescription rather than the unmade choice, which is worth
        // improving — but the failure keys and copy are settled (ADR-0002), so not here.
        LensRangeType = answers.LensRangeType ?? Contracts.Common.LensRangeType.Custom,
        PresetCatalogueId = answers.PresetCatalogueId,
        LensOptionLeftId = answers.LensOptionLeftId,
        LensOptionRightId = answers.LensOptionRightId,
        CustomSphereLeft = answers.CustomSphereLeft,
        CustomCylinderLeft = answers.CustomCylinderLeft,
        CustomAxisLeft = answers.CustomAxisLeft,
        CustomAddPowerLeft = answers.CustomAddPowerLeft,
        CustomSphereRight = answers.CustomSphereRight,
        CustomCylinderRight = answers.CustomCylinderRight,
        CustomAxisRight = answers.CustomAxisRight,
        CustomAddPowerRight = answers.CustomAddPowerRight,
        LensTypeRefId = answers.LensTypeRefId,
        LensTypeOtherText = answers.LensTypeOtherText,
        OrderFromDotGlasses = answers.OrderFromDotGlasses,
        PupilDistanceMm = answers.PupilDistanceMm,
        PresetPupilDistanceBucket = answers.PresetPupilDistanceBucket,
        ChildrensFrame = answers.ChildrensFrame,

        // No colour chosen is the same story: the request's field is non-nullable, so an unanswered
        // form sends the default. Nothing is invented to fill the gap — the default is not a real
        // FrameColour id, so it resolves to nothing in the reference-data snapshot and
        // ConsultationRules rejects it keyed on FrameColourRefId, which is the field the admin or
        // technician has to go back and answer.
        FrameColourRefId = answers.FrameColourRefId.GetValueOrDefault(),
        FrameColourOtherText = answers.FrameColourOtherText,
        FrameCoverage = answers.FrameCoverage,
        CoatingRefIds = answers.CoatingRefIds,

        HardCaseSold = answers.HardCaseSold,
        HardCaseColourRefId = answers.HardCaseSold ? answers.HardCaseColourRefId : null,
        HardCaseOtherColourText = answers.HardCaseSold ? answers.HardCaseOtherColourText : null,
    };
}

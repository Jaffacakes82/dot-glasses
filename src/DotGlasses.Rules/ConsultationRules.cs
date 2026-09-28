using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules;

/// <summary>
/// The one entry point per consultation type, per ADR-0002. Rules are composed internally from
/// per-topic functions (referral, lens range, coating set, frame, hard case, occupation) but
/// exposed request-DTO-shaped, because <see cref="RuleFailure.Key"/> is a request-DTO property
/// name and both callers — the Field App's pre-submit check and the server's create endpoint —
/// hold the request, not the topics.
///
/// The per-topic functions stay private on purpose: a test written against one of them would pin
/// the composition rather than the behaviour, and the composition is exactly what the remaining
/// migration batches change. Test through <see cref="Check(CreateSaleRequest, ReferenceDataSnapshot)"/>
/// and its siblings — see the spec's Testing Decisions.
///
/// <b>Migration complete.</b> Ticket 09 moved occupation, "referred or treated", frame colour,
/// hard case and reason-not-purchased here; ticket 10 moved the lens range — both branches, the
/// axis and power constraints, the lens-type requirement and pupil distance; ticket 11 moved the
/// Sale's <b>Coating set</b> and the Test/Lead's <b>Coating preference</b>; ticket 12 moved the
/// scalar checks (see <see cref="Scalars(CreateTestRequest)"/>) and deleted the three
/// FluentValidation validators that used to wrap this. Every consultation rule now lives here, and
/// the two named below really are the whole remainder.
///
/// <b>Two consultation rules can never live here</b>, and the synchronous snapshot-only signature
/// is what keeps that honest: an id naming a source Test (on a Lead) or a source Lead (on a Sale)
/// has to be looked up as a specific hierarchy-scoped row. That is I/O against scoped data, not a
/// fact about the reference-data library, so it stays on the server — today in LeadsController and
/// SalesController, which add the same SourceTestId/SourceLeadId-keyed failure the deleted
/// validators did. Don't widen this surface to take a repository or return a Task to accommodate
/// them.
/// </summary>
public static class ConsultationRules
{
    public static RuleResult Check(CreateTestRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Scalars(request)
                .Concat(Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot))
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(LensRange(
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                    request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: false, presetBucketMessageNamesTheBranch: false, snapshot))
                .Concat(CoatingPreference(
                    request.CoatingPreferenceRefId,
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    availabilityBeforeActiveItem: true, snapshot)));

    public static RuleResult Check(CreateLeadRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Scalars(request)
                .Concat(Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot))
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(ReasonNotPurchased(request.ReasonNotPurchasedRefId, request.ReasonNotPurchasedOtherText, snapshot))
                .Concat(LensRange(
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                    request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: false, presetBucketMessageNamesTheBranch: true, snapshot))
                .Concat(CoatingPreference(
                    request.CoatingPreferenceRefId,
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    availabilityBeforeActiveItem: false, snapshot)));

    public static RuleResult Check(CreateSaleRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Scalars(request)
                .Concat(Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot))
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(FrameColour(request.FrameColourRefId, request.FrameColourOtherText, snapshot))
                .Concat(HardCase(request.HardCaseSold, request.HardCaseColourRefId, request.HardCaseOtherColourText, snapshot))
                // LensRangeType is non-nullable on a Sale, so the "not chosen yet" branch below is
                // unreachable from here — a Sale always names its lens range.
                .Concat(LensRange(
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                    request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: true, presetBucketMessageNamesTheBranch: true, snapshot))
                .Concat(CoatingSet(
                    request.CoatingRefIds,
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    snapshot)));

    /// <summary>
    /// The checks the reference-data snapshot has no opinion on: an id that was actually filled
    /// in, a string inside its column's length, an enum value that is one of the enum's members,
    /// and an age a human could plausibly be. They ran as FluentValidation <c>RuleFor</c> chains on
    /// the three deleted validators (ticket 12) and moved here for the reason ADR-0002 gives for
    /// every other topic: the Field App has to be able to answer them offline too, and a rule the
    /// device cannot check is a rule a technician discovers at sync time.
    ///
    /// <b>The messages below are FluentValidation's generated copy, reproduced deliberately and
    /// verbatim</b> — the spaced display name ("Full Name" for FullName), the interpolated actual
    /// and permitted lengths, the trailing "You entered ..." clause. They are what clients already
    /// receive, so reproducing them is what keeps this refactor invisible from outside; they are
    /// pinned character-for-character by ConsultationRulesTests. They read oddly next to the
    /// hand-written copy elsewhere in this file, and that is the cost of not changing them.
    ///
    /// Which check applies to which request is <em>not</em> uniform, and the gaps are pre-existing
    /// drift preserved on purpose rather than tidied: only a Test length-caps LensTypeOtherText,
    /// only a Lead requires PhoneNumber, and only a Sale range-checks its LensRangeType — a Lead
    /// carries the same nullable enum and has never checked it. Harmonise them as their own
    /// decision if it is ever worth making.
    /// </summary>
    private static IEnumerable<RuleFailure> Scalars(CreateTestRequest request) =>
        NotEmpty(request.Id, IdKey, "Id")
            .Concat(InEnum(request.Gender, GenderKey, "Gender"))
            .Concat(InEnum(request.Outcome, nameof(CreateTestRequest.Outcome), "Outcome"))
            .Concat(AgeYears(request.AgeYears))
            .Concat(MaximumLength(request.OccupationOtherText, OccupationOtherTextKey, "Occupation Other Text", 200))
            .Concat(MaximumLength(request.ReferralOtherText, ReferralOtherTextKey, "Referral Other Text", 200))
            .Concat(MaximumLength(request.ReferralLocationFreeText, ReferralLocationFreeTextKey, "Referral Location Free Text", 500))
            .Concat(MaximumLength(request.LensTypeOtherText, LensTypeOtherTextKey, "Lens Type Other Text", 200));

    /// <summary>See <see cref="Scalars(CreateTestRequest)"/>.</summary>
    private static IEnumerable<RuleFailure> Scalars(CreateLeadRequest request) =>
        NotEmpty(request.Id, IdKey, "Id")
            .Concat(NotEmpty(request.FullName, FullNameKey, "Full Name"))
            .Concat(MaximumLength(request.FullName, FullNameKey, "Full Name", 200))
            .Concat(NotEmpty(request.PhoneNumber, PhoneNumberKey, "Phone Number"))
            .Concat(MaximumLength(request.PhoneNumber, PhoneNumberKey, "Phone Number", 32))
            .Concat(InEnum(request.Gender, GenderKey, "Gender"))
            .Concat(AgeYears(request.AgeYears))
            .Concat(MaximumLength(request.OccupationOtherText, OccupationOtherTextKey, "Occupation Other Text", 200))
            .Concat(MaximumLength(request.ReasonNotPurchasedOtherText, nameof(CreateLeadRequest.ReasonNotPurchasedOtherText), "Reason Not Purchased Other Text", 200))
            .Concat(MaximumLength(request.ReferralOtherText, ReferralOtherTextKey, "Referral Other Text", 200))
            .Concat(MaximumLength(request.ReferralLocationFreeText, ReferralLocationFreeTextKey, "Referral Location Free Text", 500));

    /// <summary>See <see cref="Scalars(CreateTestRequest)"/>. OrderFromDotGlasses is the one
    /// scalar carrying hand-written copy rather than FluentValidation's: it always had a
    /// WithMessage on it, because "must be equal to False" says nothing a technician can act
    /// on.</summary>
    private static IEnumerable<RuleFailure> Scalars(CreateSaleRequest request) =>
        NotEmpty(request.Id, IdKey, "Id")
            .Concat(NotEmpty(request.FullName, FullNameKey, "Full Name"))
            .Concat(MaximumLength(request.FullName, FullNameKey, "Full Name", 200))
            .Concat(MaximumLength(request.PhoneNumber, PhoneNumberKey, "Phone Number", 32))
            .Concat(InEnum(request.Gender, GenderKey, "Gender"))
            .Concat(AgeYears(request.AgeYears))
            .Concat(InEnum(request.LensRangeType, LensRangeTypeKey, "Lens Range Type"))
            .Concat(InEnum(request.FrameCoverage, nameof(CreateSaleRequest.FrameCoverage), "Frame Coverage"))
            .Concat(MaximumLength(request.OccupationOtherText, OccupationOtherTextKey, "Occupation Other Text", 200))
            .Concat(MaximumLength(request.FrameColourOtherText, nameof(CreateSaleRequest.FrameColourOtherText), "Frame Colour Other Text", 200))
            .Concat(MaximumLength(request.HardCaseOtherColourText, nameof(CreateSaleRequest.HardCaseOtherColourText), "Hard Case Other Colour Text", 200))
            .Concat(MaximumLength(request.ReferralOtherText, ReferralOtherTextKey, "Referral Other Text", 200))
            .Concat(MaximumLength(request.ReferralLocationFreeText, ReferralLocationFreeTextKey, "Referral Location Free Text", 500))
            .Concat(request.OrderFromDotGlasses && request.LensRangeType != LensRangeType.Custom
                ? [new RuleFailure(nameof(CreateSaleRequest.OrderFromDotGlasses), "OrderFromDotGlasses is only meaningful when LensRangeType is Custom.")]
                : []);

    /// <summary>An id the caller actually filled in. Guid.Empty is what a missing id deserialises
    /// to, so it is indistinguishable from "not sent" and rejected as such.</summary>
    private static IEnumerable<RuleFailure> NotEmpty(Guid value, string key, string displayName) =>
        value == Guid.Empty ? [new RuleFailure(key, $"'{displayName}' must not be empty.")] : [];

    /// <summary>Whitespace counts as empty, matching the FluentValidation rule this replaces — a
    /// customer named " " is not a named customer.</summary>
    private static IEnumerable<RuleFailure> NotEmpty(string? value, string key, string displayName) =>
        string.IsNullOrWhiteSpace(value) ? [new RuleFailure(key, $"'{displayName}' must not be empty.")] : [];

    /// <summary>A column-width cap. Null and empty pass — absence is a different question, asked
    /// by <see cref="NotEmpty(string?, string, string)"/> where it is asked at all — and the limit
    /// itself is inclusive.</summary>
    private static IEnumerable<RuleFailure> MaximumLength(string? value, string key, string displayName, int max) =>
        value is { } text && text.Length > max
            ? [new RuleFailure(key, $"The length of '{displayName}' must be {max} characters or fewer. You entered {text.Length} characters.")]
            : [];

    /// <summary>An enum value that is one of the enum's own members. A number outside the set
    /// arrives whenever a client sends an integer the server's copy of the enum has never heard
    /// of, and the message quotes it back because that number is the only clue to what was
    /// sent.</summary>
    private static IEnumerable<RuleFailure> InEnum<TEnum>(TEnum value, string key, string displayName)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value)
            ? []
            : [new RuleFailure(key, $"'{displayName}' has a range of values which does not include '{value}'.")];

    /// <summary>Optional on all three requests, range-checked whenever it is given. 120 is a
    /// plausibility ceiling, not a medical one.</summary>
    private static IEnumerable<RuleFailure> AgeYears(int? ageYears) =>
        ageYears is { } age && (age < 0 || age > 120)
            ? [new RuleFailure(AgeYearsKey, $"'Age Years' must be between 0 and 120. You entered {age}.")]
            : [];

    /// <summary>Optional on all three: no occupation recorded is a valid consultation.</summary>
    private static IEnumerable<RuleFailure> Occupation(Guid? occupationRefId, string? occupationOtherText, ReferenceDataSnapshot snapshot) =>
        occupationRefId is null
            ? []
            : ChosenItem(
                occupationRefId, occupationOtherText, ReferenceDataCategory.Occupation, snapshot,
                OccupationRefIdKey, "OccupationRefId must reference an existing, active Occupation reference-data item.",
                OccupationOtherTextKey, "OccupationOtherText is required when Occupation is \"Other\".");

    /// <summary>"Referred or treated" per <c>CONTEXT.md</c>: an explicit flag, orthogonal to
    /// Outcome and not gated on any particular outcome/result. The reason is required whenever the
    /// flag is set, whether the patient was referred out or treated in-house; only the location
    /// requirement flips on TreatedInFacility, because treating in-house names no external place.
    /// Every referral field must stay empty when the flag is clear.</summary>
    private static IEnumerable<RuleFailure> Referral(
        bool referredOrTreated, Guid? referralReasonRefId, string? referralOtherText,
        string? referralLocationFreeText, bool treatedInFacility, ReferenceDataSnapshot snapshot)
    {
        if (!referredOrTreated)
        {
            if (referralReasonRefId is not null || referralOtherText is not null
                || referralLocationFreeText is not null || treatedInFacility)
            {
                yield return new RuleFailure(ReferredOrTreatedKey, "Referral/treatment fields must be empty unless ReferredOrTreated is true.");
            }

            yield break;
        }

        if (referralReasonRefId is null)
        {
            yield return new RuleFailure(ReferralReasonRefIdKey, "ReferralReasonRefId is required when ReferredOrTreated is true.");
        }
        else
        {
            foreach (var failure in ChosenItem(
                referralReasonRefId, referralOtherText, ReferenceDataCategory.ReferralReason, snapshot,
                ReferralReasonRefIdKey, "ReferralReasonRefId must reference an existing, active ReferralReason reference-data item.",
                ReferralOtherTextKey, "ReferralOtherText is required when ReferralReason is \"Other\"."))
            {
                yield return failure;
            }
        }

        if (treatedInFacility)
        {
            if (!string.IsNullOrWhiteSpace(referralLocationFreeText))
            {
                yield return new RuleFailure(ReferralLocationFreeTextKey, "ReferralLocationFreeText must be empty when TreatedInFacility is true.");
            }
        }
        else if (string.IsNullOrWhiteSpace(referralLocationFreeText))
        {
            yield return new RuleFailure(ReferralLocationFreeTextKey, "ReferralLocationFreeText is required when ReferredOrTreated is true and TreatedInFacility is false.");
        }
    }

    /// <summary>Lead only, and required rather than optional — an unconverted Lead exists because
    /// something stopped the purchase, so the record always names it.</summary>
    private static IEnumerable<RuleFailure> ReasonNotPurchased(Guid reasonNotPurchasedRefId, string? reasonNotPurchasedOtherText, ReferenceDataSnapshot snapshot) =>
        ChosenItem(
            reasonNotPurchasedRefId, reasonNotPurchasedOtherText, ReferenceDataCategory.ReasonNotPurchased, snapshot,
            nameof(CreateLeadRequest.ReasonNotPurchasedRefId), "ReasonNotPurchasedRefId must reference an existing, active ReasonNotPurchased reference-data item.",
            nameof(CreateLeadRequest.ReasonNotPurchasedOtherText), "ReasonNotPurchasedOtherText is required when ReasonNotPurchased is \"Other\".");

    /// <summary>Sale only, and required — a sold pair of glasses always has a frame colour.</summary>
    private static IEnumerable<RuleFailure> FrameColour(Guid frameColourRefId, string? frameColourOtherText, ReferenceDataSnapshot snapshot) =>
        ChosenItem(
            frameColourRefId, frameColourOtherText, ReferenceDataCategory.FrameColour, snapshot,
            nameof(CreateSaleRequest.FrameColourRefId), "FrameColourRefId must reference an existing, active FrameColour reference-data item.",
            nameof(CreateSaleRequest.FrameColourOtherText), "FrameColourOtherText is required when FrameColour is \"Other\".");

    /// <summary>Sale only. The colour is required exactly when a hard case was sold, and both
    /// colour fields must stay empty when one wasn't.</summary>
    private static IEnumerable<RuleFailure> HardCase(bool hardCaseSold, Guid? hardCaseColourRefId, string? hardCaseOtherColourText, ReferenceDataSnapshot snapshot)
    {
        if (!hardCaseSold)
        {
            return hardCaseColourRefId is not null || hardCaseOtherColourText is not null
                ? [new RuleFailure(nameof(CreateSaleRequest.HardCaseSold), "HardCaseColourRefId/HardCaseOtherColourText must be empty when HardCaseSold is false.")]
                : [];
        }

        if (hardCaseColourRefId is null)
        {
            return [new RuleFailure(nameof(CreateSaleRequest.HardCaseColourRefId), "HardCaseColourRefId is required when HardCaseSold is true.")];
        }

        return ChosenItem(
            hardCaseColourRefId, hardCaseOtherColourText, ReferenceDataCategory.HardCaseColour, snapshot,
            nameof(CreateSaleRequest.HardCaseColourRefId), "HardCaseColourRefId must reference an existing, active HardCaseColour reference-data item.",
            nameof(CreateSaleRequest.HardCaseOtherColourText), "HardCaseOtherColourText is required when HardCaseColour is \"Other\".");
    }

    /// <summary>
    /// Which lenses this consultation calls for. Three mutually exclusive shapes: not chosen yet
    /// (Test/Lead only — a Sale always names one), a <b>lens set</b> (which one is
    /// PresetCatalogueId — ADR-0005), or a <b>Custom</b> prescription typed out in full. Whichever is
    /// chosen, the other shape's fields must be empty — that is what stops a half-edited form from
    /// being stored as a prescription nobody can grind.
    ///
    /// Two things genuinely differ between the three requests rather than being copy drift, and
    /// both are the same underlying rule: <paramref name="pupilDistanceRequired"/> — a Sale needs
    /// a pupil distance because the order cannot be ground without one, while a Test or Lead is
    /// often taken at a busy event with no time to measure it, so there it is optional but still
    /// range-checked if given. It governs both branches' PD field: the preset bucket and the
    /// Custom millimetre value.
    ///
    /// <paramref name="presetBucketMessageNamesTheBranch"/> is <em>not</em> a rule — it is
    /// pre-existing copy drift, preserved deliberately. The Test's out-of-range bucket message
    /// stops at the number where the Lead's and Sale's go on to name the branch and the children's
    /// frame allowance. The rule the three enforce is identical; only the sentence differs, and
    /// these are user-facing strings shown verbatim, so this batch reproduces them rather than
    /// quietly harmonising them. Harmonise it as its own decision if it is ever worth making.
    /// </summary>
    private static IEnumerable<RuleFailure> LensRange(
        LensRangeType? lensRangeType,
        Guid? presetCatalogueId, Guid? lensOptionLeftId, Guid? lensOptionRightId,
        decimal? sphereLeft, decimal? cylinderLeft, decimal? axisLeft, decimal? addLeft,
        decimal? sphereRight, decimal? cylinderRight, decimal? axisRight, decimal? addRight,
        Guid? lensTypeRefId, string? lensTypeOtherText,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool childrensFrame,
        bool pupilDistanceRequired, bool presetBucketMessageNamesTheBranch,
        ReferenceDataSnapshot snapshot)
    {
        var presetFieldsSet = presetCatalogueId is not null || lensOptionLeftId is not null || lensOptionRightId is not null;
        var customFieldsSet = sphereLeft is not null || cylinderLeft is not null || axisLeft is not null || addLeft is not null
            || sphereRight is not null || cylinderRight is not null || axisRight is not null || addRight is not null
            || lensTypeRefId is not null || lensTypeOtherText is not null;

        switch (lensRangeType)
        {
            case null:
                if (presetFieldsSet || customFieldsSet || pupilDistanceMm is not null || presetPupilDistanceBucket is not null)
                {
                    yield return new RuleFailure(LensRangeTypeKey, "Lens set and custom lens fields must be empty when LensRangeType is not set.");
                }

                break;

            case LensRangeType.LensSet:
                foreach (var failure in PresetBranch(
                    presetCatalogueId, lensOptionLeftId, lensOptionRightId, customFieldsSet,
                    pupilDistanceMm, presetPupilDistanceBucket, childrensFrame,
                    pupilDistanceRequired, presetBucketMessageNamesTheBranch, snapshot))
                {
                    yield return failure;
                }

                break;

            case LensRangeType.Custom:
                foreach (var failure in CustomBranch(
                    presetFieldsSet,
                    sphereLeft, cylinderLeft, axisLeft, addLeft,
                    sphereRight, cylinderRight, axisRight, addRight,
                    lensTypeRefId, lensTypeOtherText,
                    pupilDistanceMm, presetPupilDistanceBucket, pupilDistanceRequired, snapshot))
                {
                    yield return failure;
                }

                break;
        }
    }

    /// <summary>
    /// A range picked off a catalogue. The two lens options and the catalogue have to be
    /// consistent with each other — an option from a different catalogue is the mistake this
    /// catches — and the pupil distance is captured as a coarse bucket rather than a millimetre
    /// reading, its ceiling lowered for a children's frame.
    ///
    /// The missing-ids check reports once and stops: without all three ids there is nothing to
    /// check the options against, so continuing would report "must belong to PresetCatalogueId"
    /// about an id the technician never supplied. That short-circuit is also why
    /// <see cref="CoatingSet"/> and <see cref="CoatingPreference"/> re-test the same three ids —
    /// they need the left lens option, and must stay silent in exactly the cases this stops in.
    /// </summary>
    private static IEnumerable<RuleFailure> PresetBranch(
        Guid? presetCatalogueId, Guid? lensOptionLeftId, Guid? lensOptionRightId, bool customFieldsSet,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool childrensFrame,
        bool pupilDistanceRequired, bool bucketMessageNamesTheBranch, ReferenceDataSnapshot snapshot)
    {
        if (customFieldsSet)
        {
            yield return new RuleFailure(LensRangeTypeKey, "Custom prescription fields must be empty for a LensSet LensRangeType.");
        }

        if (presetCatalogueId is not { } catalogueId || lensOptionLeftId is not { } leftId || lensOptionRightId is not { } rightId)
        {
            yield return new RuleFailure(PresetCatalogueIdKey, "PresetCatalogueId, LensOptionLeftId and LensOptionRightId are all required for a LensSet LensRangeType.");
            yield break;
        }

        // Present *and* active, like every other reference: the server's snapshot keeps retired
        // lens sets so historical records still resolve their labels. The device's snapshot only
        // ever holds active ones, so there a retired set is simply absent and the belongs-to
        // checks below report it instead.
        if (snapshot.FindCatalogue(catalogueId) is { } lensSet)
        {
            if (!lensSet.IsActive)
            {
                yield return new RuleFailure(PresetCatalogueIdKey, "This lens set has been retired — choose another lens range.");
                yield break;
            }

            // Assigned at or above where the record is made (ADR-0005). The device's list is
            // already narrowed to its retail point, so this only ever refuses server-side — most
            // often a technician offline while an admin unassigned the set, which lands on Failed
            // records against this control.
            if (!snapshot.ReachesLocation(lensSet))
            {
                yield return new RuleFailure(PresetCatalogueIdKey, "This lens set isn't available at this retail point — choose another lens range.");
                yield break;
            }
        }

        if (!snapshot.LensOptionBelongsToCatalogue(leftId, catalogueId))
        {
            yield return new RuleFailure(LensOptionLeftIdKey, "LensOptionLeftId must belong to PresetCatalogueId.");
        }

        if (!snapshot.LensOptionBelongsToCatalogue(rightId, catalogueId))
        {
            yield return new RuleFailure(LensOptionRightIdKey, "LensOptionRightId must belong to PresetCatalogueId.");
        }

        if (pupilDistanceMm is not null)
        {
            yield return new RuleFailure(PupilDistanceMmKey, "PupilDistanceMm must be empty for a LensSet LensRangeType — use PresetPupilDistanceBucket instead.");
        }

        var maxBucket = childrensFrame ? 2 : 4;
        var bucketIsWrong = presetPupilDistanceBucket is { } bucket
            ? bucket < 0 || bucket > maxBucket
            : pupilDistanceRequired;

        if (bucketIsWrong)
        {
            yield return new RuleFailure(
                PresetPupilDistanceBucketKey,
                PresetBucketMessage(maxBucket, childrensFrame, pupilDistanceRequired, bucketMessageNamesTheBranch));
        }
    }

    /// <summary>See <see cref="LensRange"/> on why one rule has three sentences.</summary>
    private static string PresetBucketMessage(int maxBucket, bool childrensFrame, bool required, bool namesTheBranch)
    {
        var opening = required
            ? $"PresetPupilDistanceBucket is required and must be between 0 and {maxBucket}"
            : $"PresetPupilDistanceBucket must be between 0 and {maxBucket}";

        return namesTheBranch
            ? $"{opening} for a LensSet LensRangeType{(childrensFrame ? " (0-2 for a children's frame)" : "")}."
            : $"{opening}.";
    }

    /// <summary>
    /// A prescription typed out in full, with the shop's values (ADR-0007). Both spheres are
    /// required — one eye's prescription is not a prescription. Note that the missing-sphere
    /// failure reports against LensRangeType rather than the sphere fields: the branch as a whole
    /// is what is incomplete, and that client-visible message predates the per-eye helper, which is
    /// why this calls <see cref="LensPowerRules.CheckValues"/> rather than its sphere-requiring
    /// sibling. Everything else about each eye's power — allowed values, the axis agreeing with the
    /// cylinder, an add of 0.00 being no add — is <see cref="LensPowerRules"/>'s, keyed on this
    /// request's own property names.
    /// </summary>
    private static IEnumerable<RuleFailure> CustomBranch(
        bool presetFieldsSet,
        decimal? sphereLeft, decimal? cylinderLeft, decimal? axisLeft, decimal? addLeft,
        decimal? sphereRight, decimal? cylinderRight, decimal? axisRight, decimal? addRight,
        Guid? lensTypeRefId, string? lensTypeOtherText,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool pupilDistanceRequired,
        ReferenceDataSnapshot snapshot)
    {
        if (presetFieldsSet)
        {
            yield return new RuleFailure(LensRangeTypeKey, "Lens set fields must be empty for a Custom LensRangeType.");
        }

        if (sphereLeft is null || sphereRight is null)
        {
            yield return new RuleFailure(LensRangeTypeKey, "SphereLeft and SphereRight are required for a Custom LensRangeType.");
        }

        var powers = LensPowerRules.CheckValues(sphereLeft, cylinderLeft, axisLeft, addLeft, LeftEyeNames)
            .Concat(LensPowerRules.CheckValues(sphereRight, cylinderRight, axisRight, addRight, RightEyeNames))
            .Concat(LensPowerRules.LensType(
                LensPowerRules.HasAdd(addLeft) || LensPowerRules.HasAdd(addRight),
                lensTypeRefId, lensTypeOtherText, snapshot, LensTypeRefIdKey, LensTypeOtherTextKey));

        foreach (var failure in powers)
        {
            yield return failure;
        }

        if (presetPupilDistanceBucket is not null)
        {
            yield return new RuleFailure(PresetPupilDistanceBucketKey, "PresetPupilDistanceBucket must be empty for a Custom LensRangeType — use PupilDistanceMm instead.");
        }

        foreach (var failure in CustomPupilDistance(pupilDistanceMm, pupilDistanceRequired))
        {
            yield return failure;
        }
    }

    /// <summary>The Custom branch's pupil distance: required on a Sale, optional elsewhere (see
    /// <see cref="LensRange"/>), and in either case a whole millimetre inside the sellable range
    /// <see cref="LensPowerValues.PupilDistanceMmRange"/> defines. Out-of-range and non-whole are
    /// separate messages and only ever one at a time — a technician correcting 53.5 has one thing
    /// to fix, not two.</summary>
    private static IEnumerable<RuleFailure> CustomPupilDistance(decimal? pupilDistanceMm, bool required)
    {
        var range = LensPowerValues.PupilDistanceMmRange;
        var bounds = $"{range.Min:0}-{range.Max:0}mm";
        var rangeMessage = required
            ? $"PupilDistanceMm is required and must be within the standard {bounds} range for a Custom LensRangeType (manual override outside this range is a Day 2 feature)."
            : $"PupilDistanceMm must be within the standard {bounds} range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).";

        if (pupilDistanceMm is not { } pd)
        {
            if (required)
            {
                yield return new RuleFailure(PupilDistanceMmKey, rangeMessage);
            }
        }
        else if (pd < range.Min || pd > range.Max)
        {
            yield return new RuleFailure(PupilDistanceMmKey, rangeMessage);
        }
        else if (!range.Allows(pd))
        {
            yield return new RuleFailure(PupilDistanceMmKey, "PupilDistanceMm must be a whole millimetre value.");
        }
    }

    /// <summary>
    /// The Coatings on a <b>Sale</b>'s lens — a set, per <c>CONTEXT.md</c> and ADR-0001, because
    /// one lens can carry more than one at once. Which Coatings are allowed depends on the lens
    /// branch: a lens set narrows them to those configured as available for the left lens
    /// option's strength, while a Custom prescription accepts any active Coating. Pairing and
    /// exclusion rules apply universally to both.
    ///
    /// The preset arm re-tests all three preset ids because <see cref="PresetBranch"/>
    /// short-circuits without them, and this rule has to stay silent in exactly the same cases:
    /// there is no left lens option to scope by, and telling a technician who has not yet picked a
    /// lens to choose a coating would be noise on top of the real failure. A LensRangeType outside
    /// the enum reaches neither arm and so says nothing here — the validators' RuleFor.IsInEnum is
    /// what reports that.
    ///
    /// <b>A lens whose strength has no Coatings configured at all is reported against the lens</b>,
    /// not the set (ticket 11). <see cref="ReferenceDataSnapshot.IsCoatingAvailableForLensOption"/>
    /// returns false rather than throwing in that case, so it used to surface as "every coating
    /// must be configured as available for the chosen lens option" against CoatingRefIds — advice
    /// no choice of coating can satisfy, because none is available. It is a common state rather
    /// than an edge case (12 of the 16 seeded LensStrength items ship with none; see
    /// <c>docs/open-issues.md</c>), and the Field App's own pre-submit check already keys it to
    /// LensOptionLeftId with this same sentence. The server now agrees with it.
    /// </summary>
    private static IEnumerable<RuleFailure> CoatingSet(
        IReadOnlyList<Guid> coatingRefIds,
        LensRangeType? lensRangeType,
        Guid? presetCatalogueId, Guid? lensOptionLeftId, Guid? lensOptionRightId,
        ReferenceDataSnapshot snapshot)
    {
        switch (lensRangeType)
        {
            case LensRangeType.LensSet:
                if (presetCatalogueId is null || lensOptionRightId is null || lensOptionLeftId is not { } leftId)
                {
                    return [];
                }

                // Asked ahead of the set itself: when the lens offers nothing, the one thing worth
                // saying is about the lens, and "choose at least one coating" would send the
                // technician to a picker with no options in it. A left lens id that resolves to
                // nothing at all is a different failure and PresetBranch has already reported it,
                // so this stays out of its way and lets the per-coating check below speak.
                if (snapshot.FindLensOption(leftId) is { AvailableCoatingIds.Count: 0 })
                {
                    return [new RuleFailure(LensOptionLeftIdKey, "This lens has no coatings configured yet, so it can't be sold on a lens set.")];
                }

                return Coatings(coatingRefIds, restrictToLensOptionId: leftId, snapshot);

            case LensRangeType.Custom:
                return Coatings(coatingRefIds, restrictToLensOptionId: null, snapshot);

            default:
                return [];
        }
    }

    /// <summary>
    /// The set itself, once the branch has settled what "available" means.
    /// <paramref name="restrictToLensOptionId"/> narrows to the Coatings configured for that lens
    /// option's strength (lens set); null accepts any active Coating (Custom).
    ///
    /// One failure at a time, deliberately: each check returns rather than accumulating, so a set
    /// that is both duplicated and mutually excluding reports the duplicate first and the
    /// exclusion only once that is fixed. Every message here reports against CoatingRefIds, so
    /// accumulating them would stack several sentences on one control.
    /// </summary>
    private static IEnumerable<RuleFailure> Coatings(
        IReadOnlyList<Guid> coatingRefIds, Guid? restrictToLensOptionId, ReferenceDataSnapshot snapshot)
    {
        if (coatingRefIds.Count == 0)
        {
            return [new RuleFailure(CoatingRefIdsKey, "Choose at least one coating.")];
        }

        if (coatingRefIds.Distinct().Count() != coatingRefIds.Count)
        {
            return [new RuleFailure(CoatingRefIdsKey, "CoatingRefIds must not contain duplicates.")];
        }

        foreach (var coatingRefId in coatingRefIds)
        {
            if (!snapshot.IsActiveItem(coatingRefId, ReferenceDataCategory.Coating))
            {
                return [new RuleFailure(CoatingRefIdsKey, "CoatingRefIds must only reference existing, active Coating reference-data items.")];
            }

            if (restrictToLensOptionId is { } lensOptionId && !snapshot.IsCoatingAvailableForLensOption(lensOptionId, coatingRefId))
            {
                return [new RuleFailure(CoatingRefIdsKey, "Every coating must be configured as available for the chosen lens option (see Reference Data > Lens Strength).")];
            }
        }

        // Every unordered pair, because exclusion is symmetric per CONTEXT.md — AreCoatingsExcluded
        // canonicalizes the pair, so checking (i, j) also answers (j, i) and the inner loop can
        // start past i rather than re-asking the same question backwards.
        for (var i = 0; i < coatingRefIds.Count; i++)
        {
            for (var j = i + 1; j < coatingRefIds.Count; j++)
            {
                if (snapshot.AreCoatingsExcluded(coatingRefIds[i], coatingRefIds[j]))
                {
                    return [new RuleFailure(CoatingRefIdsKey, "This coating combination isn't allowed — two of the selected coatings exclude each other.")];
                }
            }
        }

        return [];
    }

    /// <summary>
    /// The single Coating a customer expressed interest in on a <b>Test</b> or <b>Lead</b> — a
    /// <b>Coating preference</b>, not a Coating set, and the distinction is the whole rule here.
    /// Per ADR-0001's scope correction a Test or Lead never carries a set: this is one optional
    /// value, recorded before any lens exists, which seeds the Sale's set on conversion. Nothing
    /// below asks about duplicates, pairing or exclusion, because a single value can't violate any
    /// of them.
    ///
    /// Optional for every LensRangeType, the unset one included — a preference can be recorded
    /// before a lens has been chosen. Availability is still scoped by the left lens option where a
    /// lens set names one, with the same three-id short-circuit
    /// <see cref="CoatingSet"/> uses, and stays keyed to CoatingPreferenceRefId: the
    /// no-coatings-configured case is reported against the lens on a Sale's set only, where
    /// choosing a coating is mandatory and so genuinely unsatisfiable.
    ///
    /// <paramref name="availabilityBeforeActiveItem"/> is <em>not</em> a rule — it is pre-existing
    /// ordering drift, preserved deliberately in the same spirit as
    /// <see cref="LensRange"/>'s presetBucketMessageNamesTheBranch. Both failures report against
    /// CoatingPreferenceRefId and a request can trip both at once, so which comes first is
    /// observable; a Test has always reported availability first and a Lead the active-item check
    /// first. Harmonise it as its own decision if it is ever worth making.
    /// </summary>
    private static IEnumerable<RuleFailure> CoatingPreference(
        Guid? coatingPreferenceRefId,
        LensRangeType? lensRangeType,
        Guid? presetCatalogueId, Guid? lensOptionLeftId, Guid? lensOptionRightId,
        bool availabilityBeforeActiveItem, ReferenceDataSnapshot snapshot)
    {
        if (coatingPreferenceRefId is not { } coatingRefId)
        {
            return [];
        }

        var unavailableForTheChosenLens = lensRangeType is LensRangeType.LensSet
            && presetCatalogueId is not null && lensOptionRightId is not null
            && lensOptionLeftId is { } leftId
            && !snapshot.IsCoatingAvailableForLensOption(leftId, coatingRefId);

        IEnumerable<RuleFailure> availability = unavailableForTheChosenLens
            ? [new RuleFailure(CoatingPreferenceRefIdKey, "CoatingPreferenceRefId is not configured as available for the chosen lens option (see Reference Data > Lens Strength).")]
            : [];

        IEnumerable<RuleFailure> activeItem = snapshot.IsActiveItem(coatingRefId, ReferenceDataCategory.Coating)
            ? []
            : [new RuleFailure(CoatingPreferenceRefIdKey, "CoatingPreferenceRefId must reference an existing, active Coating reference-data item.")];

        return availabilityBeforeActiveItem ? availability.Concat(activeItem) : activeItem.Concat(availability);
    }

    /// <summary>
    /// One dropdown answer, checked the one way every dropdown answer is checked: the id must
    /// resolve to an item that exists, is active, and sits in the expected category — a Guid that
    /// resolves to a Frame colour is not an answer to "which Occupation is this" — and an item
    /// flagged as the category's "Other" option must carry free text alongside it.
    ///
    /// The two failures are mutually exclusive by construction: a bad id short-circuits before the
    /// free-text question is asked, which is what keeps a single mistyped id from producing two
    /// messages.
    /// </summary>
    private static IEnumerable<RuleFailure> ChosenItem(
        Guid? refId, string? otherText, ReferenceDataCategory category, ReferenceDataSnapshot snapshot,
        string refIdKey, string notFoundMessage, string otherTextKey, string otherTextRequiredMessage)
    {
        if (snapshot.FindItem(refId, category) is not { IsActive: true } item)
        {
            return [new RuleFailure(refIdKey, notFoundMessage)];
        }

        return item.IsOtherOption && string.IsNullOrWhiteSpace(otherText)
            ? [new RuleFailure(otherTextKey, otherTextRequiredMessage)]
            : [];
    }

    // Id, Gender and AgeYears are spelled identically on all three requests; FullName and
    // PhoneNumber on the two that carry a customer (a Test records no name — see
    // CreateTestRequest). All five read off the request below that carries them, on the same
    // one-body-one-set-of-keys footing as the topics further down.
    private const string IdKey = nameof(CreateTestRequest.Id);
    private const string GenderKey = nameof(CreateTestRequest.Gender);
    private const string AgeYearsKey = nameof(CreateTestRequest.AgeYears);
    private const string FullNameKey = nameof(CreateLeadRequest.FullName);
    private const string PhoneNumberKey = nameof(CreateLeadRequest.PhoneNumber);

    // Occupation and referral are captured identically on all three requests, so their keys are
    // read off CreateTestRequest and used for all three — one rule body, one set of keys. C# has
    // no structural typing, so nothing but these nameof()s ties the three DTOs' property names
    // together; renaming the field on one request alone would silently detach the message from the
    // control that produced it (see RuleFailure).
    private const string OccupationRefIdKey = nameof(CreateTestRequest.OccupationRefId);
    private const string OccupationOtherTextKey = nameof(CreateTestRequest.OccupationOtherText);
    private const string ReferredOrTreatedKey = nameof(CreateTestRequest.ReferredOrTreated);
    private const string ReferralReasonRefIdKey = nameof(CreateTestRequest.ReferralReasonRefId);
    private const string ReferralOtherTextKey = nameof(CreateTestRequest.ReferralOtherText);
    private const string ReferralLocationFreeTextKey = nameof(CreateTestRequest.ReferralLocationFreeText);

    // Same story for the lens range: every field below is spelled identically on all three
    // requests, so one set of keys serves all three entry points. CreateSaleRequest.LensRangeType
    // is the one that differs — non-nullable there, nullable on the other two — but that is the
    // property's type, not its name, so the key still reads off CreateTestRequest with the rest.
    private const string LensRangeTypeKey = nameof(CreateTestRequest.LensRangeType);
    private const string PresetCatalogueIdKey = nameof(CreateTestRequest.PresetCatalogueId);
    private const string LensOptionLeftIdKey = nameof(CreateTestRequest.LensOptionLeftId);
    private const string LensOptionRightIdKey = nameof(CreateTestRequest.LensOptionRightId);
    private const string SphereLeftKey = nameof(CreateTestRequest.SphereLeft);
    private const string SphereRightKey = nameof(CreateTestRequest.SphereRight);
    private const string CylinderLeftKey = nameof(CreateTestRequest.CylinderLeft);
    private const string CylinderRightKey = nameof(CreateTestRequest.CylinderRight);
    private const string AddLeftKey = nameof(CreateTestRequest.AddLeft);
    private const string AddRightKey = nameof(CreateTestRequest.AddRight);
    private const string AxisLeftKey = nameof(CreateTestRequest.AxisLeft);
    private const string AxisRightKey = nameof(CreateTestRequest.AxisRight);
    private static readonly LensPowerNames LeftEyeNames = new(SphereLeftKey, CylinderLeftKey, AxisLeftKey, AddLeftKey);
    private static readonly LensPowerNames RightEyeNames = new(SphereRightKey, CylinderRightKey, AxisRightKey, AddRightKey);
    private const string LensTypeRefIdKey = nameof(CreateTestRequest.LensTypeRefId);
    private const string LensTypeOtherTextKey = nameof(CreateTestRequest.LensTypeOtherText);
    private const string PupilDistanceMmKey = nameof(CreateTestRequest.PupilDistanceMm);
    private const string PresetPupilDistanceBucketKey = nameof(CreateTestRequest.PresetPupilDistanceBucket);

    // The Coating keys are the one place the two shapes part company, so they read off the request
    // that actually carries each: CoatingPreferenceRefId is spelled the same on a Test and a Lead
    // and read off CreateTestRequest with the rest, while CoatingRefIds exists only on a Sale — a
    // Test or Lead has no set to name (ADR-0001's scope correction).
    private const string CoatingPreferenceRefIdKey = nameof(CreateTestRequest.CoatingPreferenceRefId);
    private const string CoatingRefIdsKey = nameof(CreateSaleRequest.CoatingRefIds);
}

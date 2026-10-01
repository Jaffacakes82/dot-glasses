using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Rules.LensSets;
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
                    request.LensRangeType, request.PresetCatalogueId,
                    request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                    request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: false, snapshot))
                .Concat(CoatingPreference(
                    request.CoatingPreferenceRefId,
                    LensSetPair(
                        request.LensRangeType, request.PresetCatalogueId,
                        request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                        request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                        request.LensTypeRefId, snapshot),
                    availabilityBeforeActiveItem: true, snapshot)));

    public static RuleResult Check(CreateLeadRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Scalars(request)
                .Concat(Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot))
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(ReasonNotPurchased(request.ReasonNotPurchasedRefId, request.ReasonNotPurchasedOtherText, snapshot))
                .Concat(PriceAwareness(request.CustomerToldPrice))
                .Concat(LensRange(
                    request.LensRangeType, request.PresetCatalogueId,
                    request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                    request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: false, snapshot))
                .Concat(CoatingPreference(
                    request.CoatingPreferenceRefId,
                    LensSetPair(
                        request.LensRangeType, request.PresetCatalogueId,
                        request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                        request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                        request.LensTypeRefId, snapshot),
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
                    request.LensRangeType, request.PresetCatalogueId,
                    request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                    request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: true, snapshot))
                .Concat(CoatingSet(
                    request.CoatingRefIds,
                    request.LensRangeType,
                    LensSetPair(
                        request.LensRangeType, request.PresetCatalogueId,
                        request.SphereLeft, request.CylinderLeft, request.AxisLeft, request.AddLeft,
                        request.SphereRight, request.CylinderRight, request.AxisRight, request.AddRight,
                        request.LensTypeRefId, snapshot),
                    snapshot)));

    /// <summary>
    /// The checks the reference-data snapshot has no opinion on: an id that was actually filled
    /// in, a string inside its column's length, an enum value that is one of the enum's members,
    /// and an age a human could plausibly be. They ran as FluentValidation <c>RuleFor</c> chains on
    /// the three deleted validators (ticket 12) and moved here for the reason ADR-0002 gives for
    /// every other topic: the Field App has to be able to answer them offline too, and a rule the
    /// device cannot check is a rule a technician discovers at sync time.
    ///
    /// <b>The messages a form control can cause are plain instructions</b> naming the control as
    /// the forms label it ("Enter the customer's full name."), like the rest of this file. Two
    /// kinds are still FluentValidation's generated copy, because no form can cause them and the
    /// value quoted back is the only clue to what a client sent: an empty Id, and an enum value
    /// outside its enum. All are pinned character-for-character by ConsultationRulesTests.
    ///
    /// Which check applies to which request is <em>not</em> uniform, and the gaps are pre-existing
    /// drift preserved on purpose rather than tidied: only a Lead requires PhoneNumber, and only a
    /// Sale range-checks its LensRangeType — a Lead
    /// carries the same nullable enum and has never checked it. Harmonise them as their own
    /// decision if it is ever worth making. (LensTypeOtherText used to be capped on a Test only;
    /// since a lens set's lens carries it onto all three records, every one now caps it, so an
    /// over-long text is a keyed failure rather than a database error.)
    /// </summary>
    private static IEnumerable<RuleFailure> Scalars(CreateTestRequest request) =>
        NotEmpty(request.Id, IdKey, "Id")
            .Concat(InEnum(request.Gender, GenderKey, "Gender"))
            .Concat(InEnum(request.Outcome, nameof(CreateTestRequest.Outcome), "Outcome"))
            .Concat(AgeYears(request.AgeYears))
            .Concat(MaximumLength(request.OccupationOtherText, OccupationOtherTextKey, "the other occupation", 200))
            .Concat(MaximumLength(request.ReferralOtherText, ReferralOtherTextKey, "the other referral reason", 200))
            .Concat(MaximumLength(request.ReferralLocationFreeText, ReferralLocationFreeTextKey, "the referral location", 500))
            .Concat(MaximumLength(request.LensTypeOtherText, LensTypeOtherTextKey, "the other lens type", 200));

    /// <summary>See <see cref="Scalars(CreateTestRequest)"/>.</summary>
    private static IEnumerable<RuleFailure> Scalars(CreateLeadRequest request) =>
        NotEmpty(request.Id, IdKey, "Id")
            .Concat(NotEmpty(request.FullName, FullNameKey, "Enter the customer's full name."))
            .Concat(MaximumLength(request.FullName, FullNameKey, "the full name", 200))
            .Concat(NotEmpty(request.PhoneNumber, PhoneNumberKey, "Enter a phone number."))
            .Concat(MaximumLength(request.PhoneNumber, PhoneNumberKey, "the phone number", 32))
            .Concat(InEnum(request.Gender, GenderKey, "Gender"))
            .Concat(AgeYears(request.AgeYears))
            .Concat(MaximumLength(request.OccupationOtherText, OccupationOtherTextKey, "the other occupation", 200))
            .Concat(MaximumLength(request.ReasonNotPurchasedOtherText, nameof(CreateLeadRequest.ReasonNotPurchasedOtherText), "the other reason", 200))
            .Concat(MaximumLength(request.ReferralOtherText, ReferralOtherTextKey, "the other referral reason", 200))
            .Concat(MaximumLength(request.ReferralLocationFreeText, ReferralLocationFreeTextKey, "the referral location", 500))
            .Concat(MaximumLength(request.LensTypeOtherText, LensTypeOtherTextKey, "the other lens type", 200));

    /// <summary>See <see cref="Scalars(CreateTestRequest)"/>. OrderFromDotGlasses is reachable only
    /// from the Admin Portal's conversion screen, which renders the checkbox whatever the lens
    /// range; the Field App hides it outside a Custom prescription.</summary>
    private static IEnumerable<RuleFailure> Scalars(CreateSaleRequest request) =>
        NotEmpty(request.Id, IdKey, "Id")
            .Concat(NotEmpty(request.FullName, FullNameKey, "Enter the customer's full name."))
            .Concat(MaximumLength(request.FullName, FullNameKey, "the full name", 200))
            .Concat(MaximumLength(request.PhoneNumber, PhoneNumberKey, "the phone number", 32))
            .Concat(InEnum(request.Gender, GenderKey, "Gender"))
            .Concat(AgeYears(request.AgeYears))
            .Concat(InEnum(request.LensRangeType, LensRangeTypeKey, "Lens Range Type"))
            .Concat(InEnum(request.FrameCoverage, nameof(CreateSaleRequest.FrameCoverage), "Frame Coverage"))
            .Concat(MaximumLength(request.OccupationOtherText, OccupationOtherTextKey, "the other occupation", 200))
            .Concat(MaximumLength(request.FrameColourOtherText, nameof(CreateSaleRequest.FrameColourOtherText), "the other frame colour", 200))
            .Concat(MaximumLength(request.HardCaseOtherColourText, nameof(CreateSaleRequest.HardCaseOtherColourText), "the other hard case colour", 200))
            .Concat(MaximumLength(request.ReferralOtherText, ReferralOtherTextKey, "the other referral reason", 200))
            .Concat(MaximumLength(request.ReferralLocationFreeText, ReferralLocationFreeTextKey, "the referral location", 500))
            .Concat(MaximumLength(request.LensTypeOtherText, LensTypeOtherTextKey, "the other lens type", 200))
            .Concat(request.OrderFromDotGlasses && request.LensRangeType != LensRangeType.Custom
                ? [new RuleFailure(nameof(CreateSaleRequest.OrderFromDotGlasses), "Only a Custom prescription can be ordered from Dot Glasses — untick \"Order this lens from Dot Glasses\".")]
                : []);

    /// <summary>An id the caller actually filled in. Guid.Empty is what a missing id deserialises
    /// to, so it is indistinguishable from "not sent" and rejected as such.</summary>
    private static IEnumerable<RuleFailure> NotEmpty(Guid value, string key, string displayName) =>
        value == Guid.Empty ? [new RuleFailure(key, $"'{displayName}' must not be empty.")] : [];

    /// <summary>Whitespace counts as empty, matching the FluentValidation rule this replaces — a
    /// customer named " " is not a named customer.</summary>
    private static IEnumerable<RuleFailure> NotEmpty(string? value, string key, string message) =>
        string.IsNullOrWhiteSpace(value) ? [new RuleFailure(key, message)] : [];

    /// <summary>A column-width cap. Null and empty pass — absence is a different question, asked
    /// by <see cref="NotEmpty(string?, string, string)"/> where it is asked at all — and the limit
    /// itself is inclusive.</summary>
    private static IEnumerable<RuleFailure> MaximumLength(string? value, string key, string what, int max) =>
        value is { } text && text.Length > max
            ? [new RuleFailure(key, $"Keep {what} to {max} characters or fewer.")]
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
            ? [new RuleFailure(AgeYearsKey, "Enter an age between 0 and 120.")]
            : [];

    /// <summary>Optional on all three: no occupation recorded is a valid consultation.</summary>
    private static IEnumerable<RuleFailure> Occupation(Guid? occupationRefId, string? occupationOtherText, ReferenceDataSnapshot snapshot) =>
        occupationRefId is null
            ? []
            : ChosenItem(
                occupationRefId, occupationOtherText, ReferenceDataCategory.Occupation, snapshot,
                OccupationRefIdKey, "Choose an occupation from the list.",
                OccupationOtherTextKey, "Say what the other occupation is.");

    /// <summary>"Referred or treated" per <c>CONTEXT.md</c>: an explicit flag, orthogonal to
    /// Outcome and not gated on any particular outcome/result. The reason is required whenever the
    /// flag is set, whether the patient was referred out or treated in-house. The location is
    /// optional — the technician often doesn't know it — and must stay empty when the customer was
    /// treated in the facility, because treating in-house names no external place. Every referral
    /// field must stay empty when the flag is clear.</summary>
    private static IEnumerable<RuleFailure> Referral(
        bool referredOrTreated, Guid? referralReasonRefId, string? referralOtherText,
        string? referralLocationFreeText, bool treatedInFacility, ReferenceDataSnapshot snapshot)
    {
        if (!referredOrTreated)
        {
            if (referralReasonRefId is not null || referralOtherText is not null
                || referralLocationFreeText is not null || treatedInFacility)
            {
                yield return new RuleFailure(ReferredOrTreatedKey, "Tick \"Referred or treated\", or clear the referral details.");
            }

            yield break;
        }

        if (referralReasonRefId is null)
        {
            yield return new RuleFailure(ReferralReasonRefIdKey, "Choose a reason for the referral or treatment.");
        }
        else
        {
            foreach (var failure in ChosenItem(
                referralReasonRefId, referralOtherText, ReferenceDataCategory.ReferralReason, snapshot,
                ReferralReasonRefIdKey, "Choose a reason for the referral or treatment.",
                ReferralOtherTextKey, "Say what the other referral reason is."))
            {
                yield return failure;
            }
        }

        if (treatedInFacility && !string.IsNullOrWhiteSpace(referralLocationFreeText))
        {
            yield return new RuleFailure(ReferralLocationFreeTextKey, "Clear the referral location, or untick \"Treated in facility\".");
        }
    }

    /// <summary>Lead only. "Has the customer been told the price?" must be answered, and either
    /// answer passes: it is a note for whoever follows the Lead up, not a gate on saving it.</summary>
    private static IEnumerable<RuleFailure> PriceAwareness(bool? customerToldPrice) =>
        customerToldPrice is null
            ? [new RuleFailure(nameof(CreateLeadRequest.CustomerToldPrice), "Choose Yes or No for \"Has the customer been told the price?\".")]
            : [];

    /// <summary>Lead only, and required rather than optional — an unconverted Lead exists because
    /// something stopped the purchase, so the record always names it.</summary>
    private static IEnumerable<RuleFailure> ReasonNotPurchased(Guid reasonNotPurchasedRefId, string? reasonNotPurchasedOtherText, ReferenceDataSnapshot snapshot) =>
        ChosenItem(
            reasonNotPurchasedRefId, reasonNotPurchasedOtherText, ReferenceDataCategory.ReasonNotPurchased, snapshot,
            nameof(CreateLeadRequest.ReasonNotPurchasedRefId), "Choose a reason not purchased.",
            nameof(CreateLeadRequest.ReasonNotPurchasedOtherText), "Say what the other reason is.");

    /// <summary>Sale only, and required — a sold pair of glasses always has a frame colour.</summary>
    private static IEnumerable<RuleFailure> FrameColour(Guid frameColourRefId, string? frameColourOtherText, ReferenceDataSnapshot snapshot) =>
        ChosenItem(
            frameColourRefId, frameColourOtherText, ReferenceDataCategory.FrameColour, snapshot,
            nameof(CreateSaleRequest.FrameColourRefId), "Choose a frame colour.",
            nameof(CreateSaleRequest.FrameColourOtherText), "Say what the other frame colour is.");

    /// <summary>Sale only. The colour is required exactly when a hard case was sold, and both
    /// colour fields must stay empty when one wasn't.</summary>
    private static IEnumerable<RuleFailure> HardCase(bool hardCaseSold, Guid? hardCaseColourRefId, string? hardCaseOtherColourText, ReferenceDataSnapshot snapshot)
    {
        if (!hardCaseSold)
        {
            return hardCaseColourRefId is not null || hardCaseOtherColourText is not null
                ? [new RuleFailure(nameof(CreateSaleRequest.HardCaseSold), "Clear the hard case colour, or tick that a hard case was sold.")]
                : [];
        }

        if (hardCaseColourRefId is null)
        {
            return [new RuleFailure(nameof(CreateSaleRequest.HardCaseColourRefId), "Choose a hard case colour.")];
        }

        return ChosenItem(
            hardCaseColourRefId, hardCaseOtherColourText, ReferenceDataCategory.HardCaseColour, snapshot,
            nameof(CreateSaleRequest.HardCaseColourRefId), "Choose a hard case colour.",
            nameof(CreateSaleRequest.HardCaseOtherColourText), "Say what the other hard case colour is.");
    }

    /// <summary>
    /// Which lenses this consultation calls for. Three mutually exclusive shapes: not chosen yet
    /// (Test/Lead only — a Sale always names one), a <b>lens set</b> (which one is
    /// PresetCatalogueId — ADR-0005), or a <b>Custom</b> prescription typed out in full. Both lens
    /// ranges record the lenses the same way — each eye's lens power and one lens type (ADR-0007) —
    /// so what separates them is PresetCatalogueId and the pupil distance's shape: a lens set names
    /// a set and a bucket, a Custom prescription neither set nor bucket but millimetres. Not chosen
    /// yet means every lens field is empty.
    ///
    /// Two things genuinely differ between the three requests rather than being copy drift, and
    /// both are the same underlying rule: <paramref name="pupilDistanceRequired"/> — a Sale needs
    /// a pupil distance because the order cannot be ground without one, while a Test or Lead is
    /// often taken at a busy event with no time to measure it, so there it is optional but still
    /// range-checked if given. It governs both branches' PD field: the preset bucket and the
    /// Custom millimetre value.
    ///
    /// The messages do not differ between the requests: missing or out of range, the instruction
    /// is the same one — choose a pupil distance from the list.
    /// </summary>
    private static IEnumerable<RuleFailure> LensRange(
        LensRangeType? lensRangeType,
        Guid? presetCatalogueId,
        decimal? sphereLeft, decimal? cylinderLeft, decimal? axisLeft, decimal? addLeft,
        decimal? sphereRight, decimal? cylinderRight, decimal? axisRight, decimal? addRight,
        Guid? lensTypeRefId, string? lensTypeOtherText,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool childrensFrame,
        bool pupilDistanceRequired,
        ReferenceDataSnapshot snapshot)
    {
        var lensSetChosen = presetCatalogueId is not null;
        var lensFieldsSet = sphereLeft is not null || cylinderLeft is not null || axisLeft is not null || addLeft is not null
            || sphereRight is not null || cylinderRight is not null || axisRight is not null || addRight is not null
            || lensTypeRefId is not null || lensTypeOtherText is not null;

        switch (lensRangeType)
        {
            case null:
                if (lensSetChosen || lensFieldsSet || pupilDistanceMm is not null || presetPupilDistanceBucket is not null)
                {
                    yield return new RuleFailure(LensRangeTypeKey, "Lens set and custom lens fields must be empty when LensRangeType is not set.");
                }

                break;

            case LensRangeType.LensSet:
                foreach (var failure in PresetBranch(
                    presetCatalogueId,
                    sphereLeft, cylinderLeft, axisLeft, addLeft,
                    sphereRight, cylinderRight, axisRight, addRight,
                    lensTypeRefId, lensTypeOtherText,
                    pupilDistanceMm, presetPupilDistanceBucket, childrensFrame,
                    pupilDistanceRequired, snapshot))
                {
                    yield return failure;
                }

                break;

            case LensRangeType.Custom:
                foreach (var failure in CustomBranch(
                    lensSetChosen,
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
    /// A range picked off a lens set. Each eye's lens power, with the pair's one lens type, has to
    /// be a lens in the chosen set (see <see cref="ChosenLenses"/>) — the record holds no pointer
    /// to the lens (ADR-0007), so power plus lens type is how the server knows the lens is one DGI
    /// makes — and the pupil distance is captured as a coarse bucket rather than a millimetre
    /// reading, its ceiling lowered for a children's frame.
    ///
    /// Two short-circuits, each reporting once and stopping: no lens set means nothing to match
    /// against, and an eye with no power means no lens chosen for it — which is also exactly how
    /// an old-shape request arrives, naming its lenses by ids the request no longer carries.
    /// Continuing past either would report unmatched powers or a missing bucket on top of the one
    /// thing the technician has to do. <see cref="CoatingSet"/> and
    /// <see cref="CoatingPreference"/> stay silent in exactly these cases (see
    /// <see cref="LensSetPair"/>).
    /// </summary>
    private static IEnumerable<RuleFailure> PresetBranch(
        Guid? presetCatalogueId,
        decimal? sphereLeft, decimal? cylinderLeft, decimal? axisLeft, decimal? addLeft,
        decimal? sphereRight, decimal? cylinderRight, decimal? axisRight, decimal? addRight,
        Guid? lensTypeRefId, string? lensTypeOtherText,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool childrensFrame,
        bool pupilDistanceRequired, ReferenceDataSnapshot snapshot)
    {
        if (presetCatalogueId is not { } catalogueId)
        {
            yield return new RuleFailure(PresetCatalogueIdKey, "PresetCatalogueId is required for a LensSet LensRangeType.");
            yield break;
        }

        // Present *and* active, like every other reference: the server's snapshot keeps retired
        // lens sets so historical records still resolve their labels. The device's snapshot only
        // ever holds active ones, so there a retired set is simply absent — it has no lenses to
        // match, and the per-eye checks below report it instead.
        var lensSet = snapshot.FindCatalogue(catalogueId);
        if (lensSet is not null)
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

        if (sphereLeft is not { } leftSphere || sphereRight is not { } rightSphere)
        {
            if (sphereLeft is null)
            {
                yield return new RuleFailure(SphereLeftKey, "Choose a lens for the left eye.");
            }

            if (sphereRight is null)
            {
                yield return new RuleFailure(SphereRightKey, "Choose a lens for the right eye.");
            }

            yield break;
        }

        foreach (var failure in ChosenLenses(
            lensSet?.LensOptions ?? [],
            leftSphere, cylinderLeft, axisLeft, addLeft,
            rightSphere, cylinderRight, axisRight, addRight,
            lensTypeRefId, lensTypeOtherText))
        {
            yield return failure;
        }

        if (pupilDistanceMm is not null)
        {
            yield return new RuleFailure(PupilDistanceMmKey, "PupilDistanceMm must be empty for a LensSet LensRangeType — use PresetPupilDistanceBucket instead.");
        }

        var maxBucket = LensPowerValues.MaxPresetPupilDistanceBucket(childrensFrame);
        var bucketIsWrong = presetPupilDistanceBucket is { } bucket
            ? bucket < 0 || bucket > maxBucket
            : pupilDistanceRequired;

        if (bucketIsWrong)
        {
            yield return new RuleFailure(
                PresetPupilDistanceBucketKey,
                PresetBucketMessage(maxBucket, childrensFrame));
        }
    }

    /// <summary>One sentence for all three requests, whether the bucket is missing or out of
    /// range: either way the thing to do is choose one from the list. A children's frame says why
    /// its ceiling is lower.</summary>
    private static string PresetBucketMessage(int maxBucket, bool childrensFrame) =>
        childrensFrame
            ? $"Choose a pupil distance between 0 and {maxBucket} — the limit for a children's frame."
            : $"Choose a pupil distance between 0 and {maxBucket}.";

    /// <summary>
    /// Whether each eye's lens power, with the pair's one lens type, is a lens in the chosen set —
    /// asked through <see cref="LensSetLenses.Match"/>, the one definition of "the same lens" the
    /// Field App and the Admin Portal pre-select with too. Three different things can be wrong,
    /// and each is said where it can be fixed:
    /// <list type="bullet">
    /// <item>An eye whose power is in the set under <em>no</em> lens type chose no lens from this
    /// set at all — reported against that eye's sphere, the key the lens dropdown renders.</item>
    /// <item>Both eyes are real lenses but no one lens type names both — a <b>mixed pair</b>
    /// (a Bifocal on one eye, single vision on the other). A record has one lens type for the pair,
    /// so this is refused, against the right eye: the Field App limits the right eye to the left
    /// eye's lens type, so that is the choice to change.</item>
    /// <item>Both eyes share a lens type, but the request names another — a client that sent the
    /// wrong lens type for the lenses it chose (null for a bifocal pair, say). Reported against the
    /// lens type itself.</item>
    /// <item>The lens type is right but its free text isn't the lens's own. A lens set record
    /// records its lens type — "Other" text included — off the lens (the left one, as
    /// <see cref="LensSetLenses.RecordedAs"/> does), so the text must be exactly what that lens
    /// carries: empty unless the lens's type is "Other". Reported against the text.</item>
    /// </list>
    /// </summary>
    private static IEnumerable<RuleFailure> ChosenLenses(
        IReadOnlyList<LensOptionSnapshot> lenses,
        decimal sphereLeft, decimal? cylinderLeft, decimal? axisLeft, decimal? addLeft,
        decimal sphereRight, decimal? cylinderRight, decimal? axisRight, decimal? addRight,
        Guid? lensTypeRefId, string? lensTypeOtherText)
    {
        var lensTypesInTheSet = lenses.Select(lens => lens.LensTypeRefId).Distinct().ToList();

        // The lens types under which this power is a lens in the set — Match asked once per lens
        // type the set holds, so "which lens is this" is only ever answered one way.
        List<Guid?> LensTypesOf(decimal sphere, decimal? cylinder, decimal? axis, decimal? add) =>
            lensTypesInTheSet.Where(lensType => LensSetLenses.Match(lenses, sphere, cylinder, axis, add, lensType) is not null).ToList();

        var leftLensTypes = LensTypesOf(sphereLeft, cylinderLeft, axisLeft, addLeft);
        var rightLensTypes = LensTypesOf(sphereRight, cylinderRight, axisRight, addRight);

        if (leftLensTypes.Count == 0)
        {
            yield return new RuleFailure(SphereLeftKey, "No lens in this lens set has the left eye's lens power — choose a lens.");
        }

        if (rightLensTypes.Count == 0)
        {
            yield return new RuleFailure(SphereRightKey, "No lens in this lens set has the right eye's lens power — choose a lens.");
        }

        if (leftLensTypes.Count == 0 || rightLensTypes.Count == 0)
        {
            yield break;
        }

        var shared = leftLensTypes.Intersect(rightLensTypes).ToList();
        if (shared.Count == 0)
        {
            yield return new RuleFailure(SphereRightKey, "Both eyes' lenses must be the same lens type — choose a right-eye lens of the left eye's type.");
        }
        else if (!shared.Contains(lensTypeRefId))
        {
            yield return new RuleFailure(LensTypeRefIdKey, "LensTypeRefId must be the chosen lenses' own lens type.");
        }
        else if (LensSetLenses.Match(lenses, sphereLeft, cylinderLeft, axisLeft, addLeft, lensTypeRefId) is { } leftLens
            && !string.Equals(TextOrNull(leftLens.LensTypeOtherText), TextOrNull(lensTypeOtherText), StringComparison.Ordinal))
        {
            yield return new RuleFailure(LensTypeOtherTextKey, "LensTypeOtherText must be the chosen lenses' own lens type text (empty unless their lens type is \"Other\").");
        }

        static string? TextOrNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// What the coating rules scope a lens set by: the lens in the chosen set matching each eye's
    /// power and the pair's lens type (<see cref="LensSetLenses.Match"/>, as
    /// <see cref="ChosenLenses"/> asks it). <see cref="ChosenPair.Applies"/> is false in exactly the
    /// cases <see cref="PresetBranch"/> short-circuits in (not a lens set, no lens set named, an eye
    /// with no power), where the coating rules stay silent. With Applies true, an eye whose lens is
    /// null matched no lens — already reported against that eye (or the lens type) — so there is no
    /// pair to offer coatings for.
    /// </summary>
    private static ChosenPair LensSetPair(
        LensRangeType? lensRangeType, Guid? presetCatalogueId,
        decimal? sphereLeft, decimal? cylinderLeft, decimal? axisLeft, decimal? addLeft,
        decimal? sphereRight, decimal? cylinderRight, decimal? axisRight, decimal? addRight,
        Guid? lensTypeRefId, ReferenceDataSnapshot snapshot)
    {
        if (lensRangeType is not LensRangeType.LensSet || presetCatalogueId is null || sphereLeft is null || sphereRight is null)
        {
            return new ChosenPair(Applies: false, Left: null, Right: null);
        }

        var lenses = snapshot.FindCatalogue(presetCatalogueId)?.LensOptions ?? [];
        return new ChosenPair(
            Applies: true,
            LensSetLenses.Match(lenses, sphereLeft, cylinderLeft, axisLeft, addLeft, lensTypeRefId),
            LensSetLenses.Match(lenses, sphereRight, cylinderRight, axisRight, addRight, lensTypeRefId));
    }

    /// <summary>See <see cref="LensSetPair"/>. <see cref="Coatings"/> is
    /// <see cref="LensSetLenses.CoatingsFor"/> for the two lenses — the one definition of what a
    /// pair offers and requires, shared with the Field App — or null while either eye has no
    /// lens.</summary>
    private readonly record struct ChosenPair(bool Applies, LensOptionSnapshot? Left, LensOptionSnapshot? Right)
    {
        public LensPairCoatings? Coatings =>
            Left is { } left && Right is { } right ? LensSetLenses.CoatingsFor(left, right) : null;
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
        bool lensSetChosen,
        decimal? sphereLeft, decimal? cylinderLeft, decimal? axisLeft, decimal? addLeft,
        decimal? sphereRight, decimal? cylinderRight, decimal? axisRight, decimal? addRight,
        Guid? lensTypeRefId, string? lensTypeOtherText,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool pupilDistanceRequired,
        ReferenceDataSnapshot snapshot)
    {
        if (lensSetChosen)
        {
            yield return new RuleFailure(LensRangeTypeKey, "Lens set fields must be empty for a Custom LensRangeType.");
        }

        if (sphereLeft is null || sphereRight is null)
        {
            yield return new RuleFailure(LensRangeTypeKey, "Choose a sphere for each eye.");
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
        var rangeMessage = $"Choose a pupil distance between {range.Min:0} and {range.Max:0} mm.";

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
    /// one lens can carry more than one at once. A record has one coating set for the pair
    /// (ADR-0007, "Coatings"), and which Coatings it may hold depends on the lens branch:
    /// <list type="bullet">
    /// <item><b>A lens set</b> offers only the coatings <em>both</em> chosen lenses come in, and
    /// <em>both</em> lenses' pairings apply — whichever lens a pairing is on, choosing its trigger
    /// means choosing its paired coating too. Both come from
    /// <see cref="LensSetLenses.CoatingsFor"/>, the definition the Field App offers coatings
    /// from, so the device can't show a coating the server then refuses.</item>
    /// <item><b>A Custom prescription</b> accepts any active Coating, with no pairings — pairings
    /// belong to lens set lenses.</item>
    /// </list>
    /// Exclusions are global and apply to both, and both need at least one coating.
    ///
    /// The lens-set arm stays silent wherever <see cref="PresetBranch"/> short-circuits (see
    /// <see cref="LensSetPair"/>): there are no lenses to scope by, and telling a technician who has
    /// not yet picked a lens to choose a coating would be noise on top of the real failure. An eye
    /// that names a power but matches no lens in the set has already been reported against the
    /// eye; the coatings are then still checked for everything that doesn't depend on the lenses
    /// (present, active, not duplicated, not excluded), just not narrowed to a pair that isn't
    /// there. A LensRangeType outside the enum reaches neither arm and so says nothing here —
    /// <see cref="Scalars(CreateSaleRequest)"/>' InEnum check is what reports that.
    ///
    /// <b>A pair that offers no coating at all is reported against the lenses</b>, not the set
    /// (ticket 11): reported against CoatingRefIds it would be advice no choice of coating can
    /// satisfy. A lens with no Coatings of its own is reported against its own eye's sphere, the
    /// key that eye's lens dropdown renders (a lens set lens is meant to carry at least one,
    /// ADR-0007, so that is a guard rather than a common state). Two lenses that each come in
    /// something but share nothing sellable are reported against the right eye, as a mixed pair
    /// is: the Field App narrows the right eye's choice to the left's, so that is the lens to
    /// change.
    /// </summary>
    private static IEnumerable<RuleFailure> CoatingSet(
        IReadOnlyList<Guid> coatingRefIds,
        LensRangeType? lensRangeType,
        ChosenPair pair,
        ReferenceDataSnapshot snapshot)
    {
        switch (lensRangeType)
        {
            case LensRangeType.LensSet:
                if (!pair.Applies)
                {
                    return [];
                }

                // Asked ahead of the set itself: when the lenses offer nothing, the one thing worth
                // saying is about the lenses, and "choose at least one coating" would send the
                // technician to a picker with no options in it.
                var lensesWithNoCoatings = new[] { (Lens: pair.Left, Key: SphereLeftKey), (Lens: pair.Right, Key: SphereRightKey) }
                    .Where(eye => eye.Lens is { CoatingIds.Count: 0 })
                    .Select(eye => new RuleFailure(eye.Key, "This lens has no coatings configured yet, so it can't be sold on a lens set."))
                    .ToList();
                if (lensesWithNoCoatings.Count > 0)
                {
                    return lensesWithNoCoatings;
                }

                var pairCoatings = pair.Coatings;
                if (pairCoatings is { Offered.Count: 0 })
                {
                    return [new RuleFailure(SphereRightKey, "No coating can be made on both of these lenses, so they can't be sold together on a lens set — choose another lens for the right eye.")];
                }

                return Coatings(coatingRefIds, pairCoatings, snapshot);

            case LensRangeType.Custom:
                return Coatings(coatingRefIds, lensSetPair: null, snapshot);

            default:
                return [];
        }
    }

    /// <summary>
    /// The set itself, once the branch has settled what "available" means.
    /// <paramref name="lensSetPair"/> is what a lens set pair offers and requires (lens set); null
    /// accepts any active Coating and enforces no pairing (Custom, or a lens set eye matching no
    /// lens).
    ///
    /// One failure at a time, deliberately: each check returns rather than accumulating, so a set
    /// that is both duplicated and mutually excluding reports the duplicate first and the
    /// exclusion only once that is fixed. Every message here reports against CoatingRefIds, so
    /// accumulating them would stack several sentences on one control.
    ///
    /// The pairing check comes after availability, so a trigger it names always has its paired
    /// coating on offer — <see cref="LensSetLenses.CoatingsFor"/> doesn't offer a trigger whose
    /// paired coating isn't — and "add Photochromic" is always advice the technician can follow.
    /// </summary>
    private static IEnumerable<RuleFailure> Coatings(
        IReadOnlyList<Guid> coatingRefIds, LensPairCoatings? lensSetPair, ReferenceDataSnapshot snapshot)
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
                return [new RuleFailure(CoatingRefIdsKey, "One of the chosen coatings isn't available any more — choose the coatings again.")];
            }

            if (lensSetPair is { } offeredOnThePair && !offeredOnThePair.Offered.Contains(coatingRefId))
            {
                return [new RuleFailure(CoatingRefIdsKey, "Every coating must be configured as available for the chosen lenses (see Lens Sets).")];
            }
        }

        // Directional (CONTEXT.md): the trigger needs its paired coating, never the reverse.
        foreach (var pairing in lensSetPair?.RequiredPairings ?? [])
        {
            if (coatingRefIds.Contains(pairing.TriggerCoatingRefId) && !coatingRefIds.Contains(pairing.PairedCoatingRefId))
            {
                var trigger = snapshot.ResolveLabel(pairing.TriggerCoatingRefId);
                var paired = snapshot.ResolveLabel(pairing.PairedCoatingRefId);
                return [new RuleFailure(CoatingRefIdsKey, $"{paired} comes with {trigger} on these lenses — add {paired}, or remove {trigger}.")];
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
    /// before a lens has been chosen. On a lens set where both eyes matched a lens it must be one
    /// the pair offers (<see cref="LensSetLenses.CoatingsFor"/>'s Offered, the same list
    /// <see cref="CoatingSet"/> holds a Sale to, ADR-0007) so the Sale a Lead converts into can
    /// honour it. No pairing is asked of it — it is one coating, and the paired coating joins it in
    /// the Sale's set. The failure stays keyed to CoatingPreferenceRefId even when the pair offers
    /// nothing: the lens-keyed reports are a Sale's only, where choosing a coating is mandatory and
    /// so genuinely unsatisfiable, whereas a preference can always be cleared.
    ///
    /// <paramref name="availabilityBeforeActiveItem"/> is <em>not</em> a rule — it is pre-existing
    /// ordering drift, preserved deliberately. Both failures report against
    /// CoatingPreferenceRefId and a request can trip both at once, so which comes first is
    /// observable; a Test has always reported availability first and a Lead the active-item check
    /// first. Harmonise it as its own decision if it is ever worth making.
    /// </summary>
    private static IEnumerable<RuleFailure> CoatingPreference(
        Guid? coatingPreferenceRefId,
        ChosenPair pair,
        bool availabilityBeforeActiveItem, ReferenceDataSnapshot snapshot)
    {
        if (coatingPreferenceRefId is not { } coatingRefId)
        {
            return [];
        }

        var unavailableForTheChosenLenses = pair.Applies
            && pair.Coatings is { } offeredOnThePair
            && !offeredOnThePair.Offered.Contains(coatingRefId);

        IEnumerable<RuleFailure> availability = unavailableForTheChosenLenses
            ? [new RuleFailure(CoatingPreferenceRefIdKey, "This coating preference isn't available for the chosen lenses — choose another, or no preference.")]
            : [];

        IEnumerable<RuleFailure> activeItem = snapshot.IsActiveItem(coatingRefId, ReferenceDataCategory.Coating)
            ? []
            : [new RuleFailure(CoatingPreferenceRefIdKey, "This coating preference isn't available any more — choose another, or no preference.")];

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
    private const string SphereLeftKey = nameof(CreateTestRequest.SphereLeft);
    private const string SphereRightKey = nameof(CreateTestRequest.SphereRight);
    private const string CylinderLeftKey = nameof(CreateTestRequest.CylinderLeft);
    private const string CylinderRightKey = nameof(CreateTestRequest.CylinderRight);
    private const string AddLeftKey = nameof(CreateTestRequest.AddLeft);
    private const string AddRightKey = nameof(CreateTestRequest.AddRight);
    private const string AxisLeftKey = nameof(CreateTestRequest.AxisLeft);
    private const string AxisRightKey = nameof(CreateTestRequest.AxisRight);
    private static readonly LensPowerNames LeftEyeNames = new(SphereLeftKey, CylinderLeftKey, AxisLeftKey, AddLeftKey)
    {
        SphereLabel = "Sphere (left)", CylinderLabel = "Cylinder (left)", AxisLabel = "Axis (left)", AddLabel = "Add power (left)",
    };
    private static readonly LensPowerNames RightEyeNames = new(SphereRightKey, CylinderRightKey, AxisRightKey, AddRightKey)
    {
        SphereLabel = "Sphere (right)", CylinderLabel = "Cylinder (right)", AxisLabel = "Axis (right)", AddLabel = "Add power (right)",
    };
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

using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
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
/// axis and power constraints, the lens-type requirement and pupil distance; ticket 11 has now
/// moved the last topic, the Sale's <b>Coating set</b> and the Test/Lead's <b>Coating
/// preference</b>. Every consultation rule that can be answered from the reference-data snapshot
/// now lives here, so the two rules named below really are the whole remainder.
///
/// DotGlasses.Web.Validation's three FluentValidation validators still wrap this until ticket 12
/// retires them, keeping their scalar RuleFor chains (length, enum and range checks the snapshot
/// has no opinion on) and the two repository-backed checks below.
///
/// <b>Two consultation rules can never live here</b>, and the synchronous snapshot-only signature
/// is what keeps that honest: CreateLeadRequestValidator.ValidateSourceTestAsync and
/// CreateSaleRequestValidator.ValidateSourceLeadAsync check a specific Test/Lead row through
/// IVisionTestRepository/ILeadRepository. That is I/O against hierarchy-scoped data, not a fact
/// about the reference-data library, so it stays on the server in the controller or service layer.
/// Don't widen this surface to take a repository or return a Task to accommodate them.
/// </summary>
public static class ConsultationRules
{
    public static RuleResult Check(CreateTestRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot)
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(LensRange(
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    request.CustomSphereLeft, request.CustomCylinderLeft, request.CustomAxisLeft, request.CustomAddPowerLeft,
                    request.CustomSphereRight, request.CustomCylinderRight, request.CustomAxisRight, request.CustomAddPowerRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: false, presetBucketMessageNamesTheBranch: false, snapshot))
                .Concat(CoatingPreference(
                    request.CoatingPreferenceRefId,
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    availabilityBeforeActiveItem: true, snapshot)));

    public static RuleResult Check(CreateLeadRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot)
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(ReasonNotPurchased(request.ReasonNotPurchasedRefId, request.ReasonNotPurchasedOtherText, snapshot))
                .Concat(LensRange(
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    request.CustomSphereLeft, request.CustomCylinderLeft, request.CustomAxisLeft, request.CustomAddPowerLeft,
                    request.CustomSphereRight, request.CustomCylinderRight, request.CustomAxisRight, request.CustomAddPowerRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: false, presetBucketMessageNamesTheBranch: true, snapshot))
                .Concat(CoatingPreference(
                    request.CoatingPreferenceRefId,
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    availabilityBeforeActiveItem: false, snapshot)));

    public static RuleResult Check(CreateSaleRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot)
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(FrameColour(request.FrameColourRefId, request.FrameColourOtherText, snapshot))
                .Concat(HardCase(request.HardCaseSold, request.HardCaseColourRefId, request.HardCaseOtherColourText, snapshot))
                // LensRangeType is non-nullable on a Sale, so the "not chosen yet" branch below is
                // unreachable from here — a Sale always names its lens range.
                .Concat(LensRange(
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    request.CustomSphereLeft, request.CustomCylinderLeft, request.CustomAxisLeft, request.CustomAddPowerLeft,
                    request.CustomSphereRight, request.CustomCylinderRight, request.CustomAxisRight, request.CustomAddPowerRight,
                    request.LensTypeRefId, request.LensTypeOtherText,
                    request.PupilDistanceMm, request.PresetPupilDistanceBucket, request.ChildrensFrame,
                    pupilDistanceRequired: true, presetBucketMessageNamesTheBranch: true, snapshot))
                .Concat(CoatingSet(
                    request.CoatingRefIds,
                    request.LensRangeType, request.PresetCatalogueId, request.LensOptionLeftId, request.LensOptionRightId,
                    snapshot)));

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
    /// (Test/Lead only — a Sale always names one), a <b>preset</b> range picked off an
    /// admin-curated catalogue, or a <b>Custom</b> prescription typed out in full. Whichever is
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
        decimal? customSphereLeft, decimal? customCylinderLeft, decimal? customAxisLeft, decimal? customAddPowerLeft,
        decimal? customSphereRight, decimal? customCylinderRight, decimal? customAxisRight, decimal? customAddPowerRight,
        Guid? lensTypeRefId, string? lensTypeOtherText,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool childrensFrame,
        bool pupilDistanceRequired, bool presetBucketMessageNamesTheBranch,
        ReferenceDataSnapshot snapshot)
    {
        var presetFieldsSet = presetCatalogueId is not null || lensOptionLeftId is not null || lensOptionRightId is not null;
        var customFieldsSet = customSphereLeft is not null || customCylinderLeft is not null || customAxisLeft is not null || customAddPowerLeft is not null
            || customSphereRight is not null || customCylinderRight is not null || customAxisRight is not null || customAddPowerRight is not null
            || lensTypeRefId is not null || lensTypeOtherText is not null;

        switch (lensRangeType)
        {
            case null:
                if (presetFieldsSet || customFieldsSet || pupilDistanceMm is not null || presetPupilDistanceBucket is not null)
                {
                    yield return new RuleFailure(LensRangeTypeKey, "Preset/custom lens fields must be empty when LensRangeType is not set.");
                }

                break;

            case LensRangeType.SixLensSet or LensRangeType.NineLensSet:
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
                    customSphereLeft, customCylinderLeft, customAxisLeft, customAddPowerLeft,
                    customSphereRight, customCylinderRight, customAxisRight, customAddPowerRight,
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
            yield return new RuleFailure(LensRangeTypeKey, "Custom prescription fields must be empty for a preset LensRangeType.");
        }

        if (presetCatalogueId is not { } catalogueId || lensOptionLeftId is not { } leftId || lensOptionRightId is not { } rightId)
        {
            yield return new RuleFailure(PresetCatalogueIdKey, "PresetCatalogueId, LensOptionLeftId and LensOptionRightId are all required for a preset LensRangeType.");
            yield break;
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
            yield return new RuleFailure(PupilDistanceMmKey, "PupilDistanceMm must be empty for a preset LensRangeType — use PresetPupilDistanceBucket instead.");
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
            ? $"{opening} for a preset LensRangeType{(childrensFrame ? " (0-2 for a children's frame)" : "")}."
            : $"{opening}.";
    }

    /// <summary>
    /// A prescription typed out in full. Both spheres are required — one eye's prescription is not
    /// a prescription — while cylinder, axis and add power are each optional but constrained if
    /// given. Note that the missing-sphere failure reports against LensRangeType rather than the
    /// sphere fields: the branch as a whole is what is incomplete.
    /// </summary>
    private static IEnumerable<RuleFailure> CustomBranch(
        bool presetFieldsSet,
        decimal? customSphereLeft, decimal? customCylinderLeft, decimal? customAxisLeft, decimal? customAddPowerLeft,
        decimal? customSphereRight, decimal? customCylinderRight, decimal? customAxisRight, decimal? customAddPowerRight,
        Guid? lensTypeRefId, string? lensTypeOtherText,
        decimal? pupilDistanceMm, int? presetPupilDistanceBucket, bool pupilDistanceRequired,
        ReferenceDataSnapshot snapshot)
    {
        if (presetFieldsSet)
        {
            yield return new RuleFailure(LensRangeTypeKey, "Preset fields must be empty for a Custom LensRangeType.");
        }

        if (customSphereLeft is null || customSphereRight is null)
        {
            yield return new RuleFailure(LensRangeTypeKey, "CustomSphereLeft and CustomSphereRight are required for a Custom LensRangeType.");
        }

        var powers = CustomPower(customSphereLeft, CustomSphereLeftKey, -10m, 10m, 0.25m)
            .Concat(CustomPower(customSphereRight, CustomSphereRightKey, -10m, 10m, 0.25m))
            .Concat(CustomPower(customCylinderLeft, CustomCylinderLeftKey, -10m, 10m, 0.25m))
            .Concat(CustomPower(customCylinderRight, CustomCylinderRightKey, -10m, 10m, 0.25m))
            .Concat(CustomPower(customAddPowerLeft, CustomAddPowerLeftKey, 0m, 3m, 0.25m))
            .Concat(CustomPower(customAddPowerRight, CustomAddPowerRightKey, 0m, 3m, 0.25m))
            .Concat(CustomAxis(customAxisLeft, CustomAxisLeftKey))
            .Concat(CustomAxis(customAxisRight, CustomAxisRightKey))
            .Concat(LensType(customAddPowerLeft, customAddPowerRight, lensTypeRefId, lensTypeOtherText, snapshot));

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

    /// <summary>Sphere/Cylinder/Add-power are physical lens-grinding constraints, not
    /// admin-curated reference data — checked in code against the ground ranges, not a lookup
    /// table. The increment is the rule most easily got wrong: a value inside the range but off
    /// the quarter-dioptre step is not grindable, so range and step are one question with one
    /// message.</summary>
    private static IEnumerable<RuleFailure> CustomPower(decimal? value, string propertyName, decimal min, decimal max, decimal step) =>
        value is { } v && (v < min || v > max || (v - min) % step != 0)
            ? [new RuleFailure(propertyName, $"{propertyName} must be between {min} and {max} in {step} increments.")]
            : [];

    /// <summary>Axis is a bearing in whole degrees; 180 is in range and 180.5 is not a bearing
    /// anyone can grind.</summary>
    private static IEnumerable<RuleFailure> CustomAxis(decimal? value, string propertyName) =>
        value is { } v && (v < 0 || v > 180 || v != Math.Truncate(v))
            ? [new RuleFailure(propertyName, $"{propertyName} must be a whole number of degrees between 0 and 180.")]
            : [];

    /// <summary>Asked exactly once an eye carries two distinct powers — a base sphere plus an add
    /// power, which is what makes the lens bifocal or progressive and so needs naming. Required in
    /// that case; both lens-type fields must stay empty otherwise.</summary>
    private static IEnumerable<RuleFailure> LensType(
        decimal? customAddPowerLeft, decimal? customAddPowerRight,
        Guid? lensTypeRefId, string? lensTypeOtherText, ReferenceDataSnapshot snapshot)
    {
        var hasTwoPowers = customAddPowerLeft is not null || customAddPowerRight is not null;
        if (!hasTwoPowers)
        {
            return lensTypeRefId is not null || lensTypeOtherText is not null
                ? [new RuleFailure(LensTypeRefIdKey, "LensTypeRefId/LensTypeOtherText must be empty unless an add power is set.")]
                : [];
        }

        if (lensTypeRefId is null)
        {
            return [new RuleFailure(LensTypeRefIdKey, "LensTypeRefId is required when an add power is set (two distinct powers on that eye).")];
        }

        return ChosenItem(
            lensTypeRefId, lensTypeOtherText, ReferenceDataCategory.LensType, snapshot,
            LensTypeRefIdKey, "LensTypeRefId must reference an existing, active LensType reference-data item.",
            LensTypeOtherTextKey, "LensTypeOtherText is required when LensType is \"Other\".");
    }

    /// <summary>The Custom branch's pupil distance: required on a Sale, optional elsewhere (see
    /// <see cref="LensRange"/>), and in either case a whole millimetre inside the sellable
    /// 54-74mm range. Out-of-range and non-whole are separate messages and only ever one at a
    /// time — a technician correcting 53.5 has one thing to fix, not two.</summary>
    private static IEnumerable<RuleFailure> CustomPupilDistance(decimal? pupilDistanceMm, bool required)
    {
        var rangeMessage = required
            ? "PupilDistanceMm is required and must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature)."
            : "PupilDistanceMm must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).";

        if (pupilDistanceMm is not { } pd)
        {
            if (required)
            {
                yield return new RuleFailure(PupilDistanceMmKey, rangeMessage);
            }
        }
        else if (pd < 54 || pd > 74)
        {
            yield return new RuleFailure(PupilDistanceMmKey, rangeMessage);
        }
        else if (pd != Math.Truncate(pd))
        {
            yield return new RuleFailure(PupilDistanceMmKey, "PupilDistanceMm must be a whole millimetre value.");
        }
    }

    /// <summary>
    /// The Coatings on a <b>Sale</b>'s lens — a set, per <c>CONTEXT.md</c> and ADR-0001, because
    /// one lens can carry more than one at once. Which Coatings are allowed depends on the lens
    /// branch: a preset range narrows them to those configured as available for the left lens
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
            case LensRangeType.SixLensSet or LensRangeType.NineLensSet:
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
                    return [new RuleFailure(LensOptionLeftIdKey, "This lens has no coatings configured yet, so it can't be sold on a preset range.")];
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
    /// option's strength (preset); null accepts any active Coating (Custom).
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
    /// preset range names one, with the same three-id short-circuit
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

        var unavailableForTheChosenLens = lensRangeType is LensRangeType.SixLensSet or LensRangeType.NineLensSet
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
    private const string CustomSphereLeftKey = nameof(CreateTestRequest.CustomSphereLeft);
    private const string CustomSphereRightKey = nameof(CreateTestRequest.CustomSphereRight);
    private const string CustomCylinderLeftKey = nameof(CreateTestRequest.CustomCylinderLeft);
    private const string CustomCylinderRightKey = nameof(CreateTestRequest.CustomCylinderRight);
    private const string CustomAddPowerLeftKey = nameof(CreateTestRequest.CustomAddPowerLeft);
    private const string CustomAddPowerRightKey = nameof(CreateTestRequest.CustomAddPowerRight);
    private const string CustomAxisLeftKey = nameof(CreateTestRequest.CustomAxisLeft);
    private const string CustomAxisRightKey = nameof(CreateTestRequest.CustomAxisRight);
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

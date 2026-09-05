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
/// <b>Partially migrated.</b> Ticket 09 has moved occupation, "referred or treated", frame colour,
/// hard case and reason-not-purchased here; lens range (ticket 10) and the Coating set/preference
/// (ticket 11) are still enforced only by DotGlasses.Web.Validation's three FluentValidation
/// validators, which now call this for the topics above and keep the rest. Until ticket 12 those
/// validators remain the complete server-side check — <b>a caller that runs this alone is not yet
/// running every consultation rule</b>.
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
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot)));

    public static RuleResult Check(CreateLeadRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot)
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(ReasonNotPurchased(request.ReasonNotPurchasedRefId, request.ReasonNotPurchasedOtherText, snapshot)));

    public static RuleResult Check(CreateSaleRequest request, ReferenceDataSnapshot snapshot) =>
        RuleResult.From(
            Occupation(request.OccupationRefId, request.OccupationOtherText, snapshot)
                .Concat(Referral(request.ReferredOrTreated, request.ReferralReasonRefId, request.ReferralOtherText, request.ReferralLocationFreeText, request.TreatedInFacility, snapshot))
                .Concat(FrameColour(request.FrameColourRefId, request.FrameColourOtherText, snapshot))
                .Concat(HardCase(request.HardCaseSold, request.HardCaseColourRefId, request.HardCaseOtherColourText, snapshot)));

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
}

using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests;

/// <summary>
/// The rules moved in ticket 09 — occupation, "referred or treated", reason not purchased, frame
/// colour and hard case — exercised through <see cref="ConsultationRules"/>'s three entry points,
/// never through the per-topic functions behind them: those are private precisely so a test can't
/// pin the composition the remaining migration batches are going to change.
///
/// The snapshot is a plain literal in every case. Occupation and referral are checked on the Test
/// request and only smoke-checked on Lead/Sale, because there is one rule body behind all three
/// entry points and a third copy of each case would be testing C#'s overload resolution rather
/// than a rule.
/// </summary>
public class ConsultationRulesTests
{
    private static readonly Guid ActiveOccupation = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid RetiredOccupation = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid OtherOccupation = Guid.Parse("00000000-0000-0000-0000-0000000000a3");

    private static readonly Guid ActiveReferralReason = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid RetiredReferralReason = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid OtherReferralReason = Guid.Parse("00000000-0000-0000-0000-0000000000b3");

    private static readonly Guid ActiveReasonNotPurchased = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid OtherReasonNotPurchased = Guid.Parse("00000000-0000-0000-0000-0000000000c2");

    private static readonly Guid ActiveFrameColour = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    private static readonly Guid RetiredFrameColour = Guid.Parse("00000000-0000-0000-0000-0000000000d2");
    private static readonly Guid OtherFrameColour = Guid.Parse("00000000-0000-0000-0000-0000000000d3");

    private static readonly Guid ActiveHardCaseColour = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    private static readonly Guid RetiredHardCaseColour = Guid.Parse("00000000-0000-0000-0000-0000000000e2");
    private static readonly Guid OtherHardCaseColour = Guid.Parse("00000000-0000-0000-0000-0000000000e3");

    private static readonly Guid NeverExisted = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    /// <summary>The server's filling: the whole library, retired items carrying IsActive = false.
    /// Every category this batch touches has an active item, a retired one, and the category's one
    /// "Other" option.</summary>
    private static ReferenceDataSnapshot Snapshot() => new(
        [
            new ReferenceItemSnapshot(ActiveOccupation, ReferenceDataCategory.Occupation, "Farmer", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(RetiredOccupation, ReferenceDataCategory.Occupation, "Typist", IsActive: false, IsOtherOption: false),
            new ReferenceItemSnapshot(OtherOccupation, ReferenceDataCategory.Occupation, "Other", IsActive: true, IsOtherOption: true),

            new ReferenceItemSnapshot(ActiveReferralReason, ReferenceDataCategory.ReferralReason, "Cataract", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(RetiredReferralReason, ReferenceDataCategory.ReferralReason, "Pterygium", IsActive: false, IsOtherOption: false),
            new ReferenceItemSnapshot(OtherReferralReason, ReferenceDataCategory.ReferralReason, "Other", IsActive: true, IsOtherOption: true),

            new ReferenceItemSnapshot(ActiveReasonNotPurchased, ReferenceDataCategory.ReasonNotPurchased, "Too expensive", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(OtherReasonNotPurchased, ReferenceDataCategory.ReasonNotPurchased, "Other", IsActive: true, IsOtherOption: true),

            new ReferenceItemSnapshot(ActiveFrameColour, ReferenceDataCategory.FrameColour, "Black", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(RetiredFrameColour, ReferenceDataCategory.FrameColour, "Tortoiseshell", IsActive: false, IsOtherOption: false),
            new ReferenceItemSnapshot(OtherFrameColour, ReferenceDataCategory.FrameColour, "Other", IsActive: true, IsOtherOption: true),

            new ReferenceItemSnapshot(ActiveHardCaseColour, ReferenceDataCategory.HardCaseColour, "Navy", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(RetiredHardCaseColour, ReferenceDataCategory.HardCaseColour, "Maroon", IsActive: false, IsOtherOption: false),
            new ReferenceItemSnapshot(OtherHardCaseColour, ReferenceDataCategory.HardCaseColour, "Other", IsActive: true, IsOtherOption: true),
        ],
        [],
        [],
        []);

    /// <summary>A request carrying nothing this batch's rules object to — no occupation, not
    /// referred. Lens range and Coating preference are untouched here (tickets 10/11), so their
    /// fields stay null throughout.</summary>
    private static CreateTestRequest ValidTest() => new() { Id = Guid.NewGuid() };

    private static CreateLeadRequest ValidLead() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        PhoneNumber = "+254700000000",
        ReasonNotPurchasedRefId = ActiveReasonNotPurchased,
    };

    private static CreateSaleRequest ValidSale() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        FrameColourRefId = ActiveFrameColour,
    };

    private static RuleFailure AssertSingleFailure(RuleResult result)
    {
        Assert.False(result.IsValid);
        return Assert.Single(result.Failures);
    }

    // --- Occupation -----------------------------------------------------------------------

    [Fact]
    public void Occupation_NotRecorded_IsAccepted()
    {
        // Optional on all three requests — plenty of consultations never ask.
        Assert.True(ConsultationRules.Check(ValidTest(), Snapshot()).IsValid);
    }

    [Fact]
    public void Occupation_ActiveItem_IsAccepted()
    {
        var request = ValidTest();
        request.OccupationRefId = ActiveOccupation;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void Occupation_ItemThatNeverExisted_IsRejected()
    {
        var request = ValidTest();
        request.OccupationRefId = NeverExisted;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("OccupationRefId", failure.Key);
        Assert.Equal("OccupationRefId must reference an existing, active Occupation reference-data item.", failure.Message);
    }

    [Fact]
    public void Occupation_RetiredItem_IsRejected()
    {
        // Present in the server's snapshot but inactive; absent altogether from the Field App's.
        // Both fillings have to reject it, which is why the rule asks "present and active".
        var request = ValidTest();
        request.OccupationRefId = RetiredOccupation;

        Assert.Equal("OccupationRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Occupation_ItemFromAnotherCategory_IsRejected()
    {
        // A Guid that resolves to an active Frame colour is not an answer to "which Occupation".
        var request = ValidTest();
        request.OccupationRefId = ActiveFrameColour;

        Assert.Equal("OccupationRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Occupation_OtherWithoutFreeText_IsRejected()
    {
        var request = ValidTest();
        request.OccupationRefId = OtherOccupation;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("OccupationOtherText", failure.Key);
        Assert.Equal("OccupationOtherText is required when Occupation is \"Other\".", failure.Message);
    }

    [Fact]
    public void Occupation_OtherWithWhitespaceOnlyFreeText_IsRejected()
    {
        var request = ValidTest();
        request.OccupationRefId = OtherOccupation;
        request.OccupationOtherText = "   ";

        Assert.Equal("OccupationOtherText", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Occupation_OtherWithFreeText_IsAccepted()
    {
        var request = ValidTest();
        request.OccupationRefId = OtherOccupation;
        request.OccupationOtherText = "Fisherman";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void Occupation_FreeTextWithoutAnOtherOption_IsAccepted()
    {
        // Stray free text alongside a non-"Other" choice is not something this batch rejects —
        // only the reverse is a rule.
        var request = ValidTest();
        request.OccupationRefId = ActiveOccupation;
        request.OccupationOtherText = "left over from an earlier answer";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    // --- Referred or treated --------------------------------------------------------------

    [Fact]
    public void Referral_NotReferredAndNoReferralFields_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(ValidTest(), Snapshot()).IsValid);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("otherText")]
    [InlineData("location")]
    [InlineData("treatedInFacility")]
    public void Referral_NotReferredButAReferralFieldIsSet_IsRejected(string field)
    {
        // All four fields hang off the one flag, and all four report against the flag rather than
        // against themselves — the flag is the thing the technician has to correct.
        var request = ValidTest();
        switch (field)
        {
            case "reason": request.ReferralReasonRefId = ActiveReferralReason; break;
            case "otherText": request.ReferralOtherText = "Referred to the district hospital"; break;
            case "location": request.ReferralLocationFreeText = "Kisumu District Hospital"; break;
            case "treatedInFacility": request.TreatedInFacility = true; break;
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("ReferredOrTreated", failure.Key);
        Assert.Equal("Referral/treatment fields must be empty unless ReferredOrTreated is true.", failure.Message);
    }

    [Fact]
    public void Referral_NotReferredButAnEmptyStringWasLeftInAReferralField_IsRejected()
    {
        // The emptiness check is "is not null", deliberately not IsNullOrWhiteSpace: a control the
        // technician typed in and then cleared sends "" rather than null, and that is a referral
        // field that was filled while the flag says it wasn't. Note the asymmetry with the
        // requiredness checks below, which *do* treat whitespace as absent — the two directions
        // are different questions, and tidying them into one predicate would change behaviour.
        var request = ValidTest();
        request.ReferralOtherText = string.Empty;

        Assert.Equal("ReferredOrTreated", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Referral_ReferredWithoutAReason_IsRejected()
    {
        // Referred out, so a location is still expected too — the reason and the location are
        // independent requirements, not a chain.
        var request = ValidTest();
        request.ReferredOrTreated = true;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(2, result.Failures.Count);
        Assert.Equal("ReferralReasonRefId", result.Failures[0].Key);
        Assert.Equal("ReferralReasonRefId is required when ReferredOrTreated is true.", result.Failures[0].Message);
        Assert.Equal("ReferralLocationFreeText", result.Failures[1].Key);
    }

    [Fact]
    public void Referral_TreatedInFacilityWithoutAReason_IsStillRejected()
    {
        // Per CONTEXT.md: the reason is required regardless of TreatedInFacility. Only the
        // location requirement flips.
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;

        Assert.Equal("ReferralReasonRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Referral_RetiredReason_IsRejected()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;
        request.ReferralReasonRefId = RetiredReferralReason;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("ReferralReasonRefId", failure.Key);
        Assert.Equal("ReferralReasonRefId must reference an existing, active ReferralReason reference-data item.", failure.Message);
    }

    [Fact]
    public void Referral_ReasonFromAnotherCategory_IsRejected()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;
        request.ReferralReasonRefId = ActiveOccupation;

        Assert.Equal("ReferralReasonRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Referral_OtherReasonWithoutFreeText_IsRejected()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;
        request.ReferralReasonRefId = OtherReferralReason;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("ReferralOtherText", failure.Key);
        Assert.Equal("ReferralOtherText is required when ReferralReason is \"Other\".", failure.Message);
    }

    [Fact]
    public void Referral_OtherReasonWithFreeText_IsAccepted()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;
        request.ReferralReasonRefId = OtherReferralReason;
        request.ReferralOtherText = "Suspected glaucoma";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void Referral_BadReasonIdProducesOneMessage_NotTwo()
    {
        // A mistyped id short-circuits before the "Other" free-text question is asked, so the
        // technician sees the one thing that is actually wrong.
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;
        request.ReferralReasonRefId = NeverExisted;

        Assert.Single(ConsultationRules.Check(request, Snapshot()).Failures);
    }

    [Fact]
    public void Referral_TreatedInFacilityWithALocation_IsRejected()
    {
        // Treated in-house names no external place, so the location field is suppressed.
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;
        request.ReferralReasonRefId = ActiveReferralReason;
        request.ReferralLocationFreeText = "Kisumu District Hospital";

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("ReferralLocationFreeText", failure.Key);
        Assert.Equal("ReferralLocationFreeText must be empty when TreatedInFacility is true.", failure.Message);
    }

    [Fact]
    public void Referral_TreatedInFacilityWithoutALocation_IsAccepted()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;
        request.ReferralReasonRefId = ActiveReferralReason;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void Referral_ReferredOutWithoutALocation_IsRejected()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.ReferralReasonRefId = ActiveReferralReason;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("ReferralLocationFreeText", failure.Key);
        Assert.Equal("ReferralLocationFreeText is required when ReferredOrTreated is true and TreatedInFacility is false.", failure.Message);
    }

    [Fact]
    public void Referral_ReferredOutWithWhitespaceOnlyLocation_IsRejected()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.ReferralReasonRefId = ActiveReferralReason;
        request.ReferralLocationFreeText = "   ";

        Assert.Equal("ReferralLocationFreeText", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Referral_ReferredOutWithALocation_IsAccepted()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.ReferralReasonRefId = ActiveReferralReason;
        request.ReferralLocationFreeText = "Kisumu District Hospital";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void Referral_IsTheSameRuleOnALead()
    {
        var request = ValidLead();
        request.ReferredOrTreated = true;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(["ReferralReasonRefId", "ReferralLocationFreeText"], result.Failures.Select(f => f.Key));
    }

    [Fact]
    public void Referral_IsTheSameRuleOnASale()
    {
        var request = ValidSale();
        request.ReferredOrTreated = true;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(["ReferralReasonRefId", "ReferralLocationFreeText"], result.Failures.Select(f => f.Key));
    }

    [Fact]
    public void Occupation_IsTheSameRuleOnALeadAndASale()
    {
        var lead = ValidLead();
        lead.OccupationRefId = OtherOccupation;
        var sale = ValidSale();
        sale.OccupationRefId = OtherOccupation;

        Assert.Equal("OccupationOtherText", AssertSingleFailure(ConsultationRules.Check(lead, Snapshot())).Key);
        Assert.Equal("OccupationOtherText", AssertSingleFailure(ConsultationRules.Check(sale, Snapshot())).Key);
    }

    // --- Reason not purchased (Lead) ------------------------------------------------------

    [Fact]
    public void ReasonNotPurchased_ActiveItem_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(ValidLead(), Snapshot()).IsValid);
    }

    [Fact]
    public void ReasonNotPurchased_Unset_IsRejected()
    {
        // Required rather than optional: an unconverted Lead exists because something stopped the
        // purchase, so an empty Guid is a missing answer, not "not asked".
        var request = ValidLead();
        request.ReasonNotPurchasedRefId = Guid.Empty;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("ReasonNotPurchasedRefId", failure.Key);
        Assert.Equal("ReasonNotPurchasedRefId must reference an existing, active ReasonNotPurchased reference-data item.", failure.Message);
    }

    [Fact]
    public void ReasonNotPurchased_ItemFromAnotherCategory_IsRejected()
    {
        var request = ValidLead();
        request.ReasonNotPurchasedRefId = ActiveOccupation;

        Assert.Equal("ReasonNotPurchasedRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void ReasonNotPurchased_OtherWithoutFreeText_IsRejected()
    {
        var request = ValidLead();
        request.ReasonNotPurchasedRefId = OtherReasonNotPurchased;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("ReasonNotPurchasedOtherText", failure.Key);
        Assert.Equal("ReasonNotPurchasedOtherText is required when ReasonNotPurchased is \"Other\".", failure.Message);
    }

    [Fact]
    public void ReasonNotPurchased_OtherWithFreeText_IsAccepted()
    {
        var request = ValidLead();
        request.ReasonNotPurchasedRefId = OtherReasonNotPurchased;
        request.ReasonNotPurchasedOtherText = "Saving for school fees";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    // --- Frame colour (Sale) --------------------------------------------------------------

    [Fact]
    public void FrameColour_ActiveItem_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(ValidSale(), Snapshot()).IsValid);
    }

    [Fact]
    public void FrameColour_Unset_IsRejected()
    {
        // Required: a sold pair of glasses always has a frame colour.
        var request = ValidSale();
        request.FrameColourRefId = Guid.Empty;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("FrameColourRefId", failure.Key);
        Assert.Equal("FrameColourRefId must reference an existing, active FrameColour reference-data item.", failure.Message);
    }

    [Fact]
    public void FrameColour_RetiredItem_IsRejected()
    {
        var request = ValidSale();
        request.FrameColourRefId = RetiredFrameColour;

        Assert.Equal("FrameColourRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void FrameColour_ItemFromAnotherCategory_IsRejected()
    {
        var request = ValidSale();
        request.FrameColourRefId = ActiveHardCaseColour;

        Assert.Equal("FrameColourRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void FrameColour_OtherWithoutFreeText_IsRejected()
    {
        var request = ValidSale();
        request.FrameColourRefId = OtherFrameColour;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("FrameColourOtherText", failure.Key);
        Assert.Equal("FrameColourOtherText is required when FrameColour is \"Other\".", failure.Message);
    }

    [Fact]
    public void FrameColour_OtherWithFreeText_IsAccepted()
    {
        var request = ValidSale();
        request.FrameColourRefId = OtherFrameColour;
        request.FrameColourOtherText = "Two-tone blue and grey";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    // --- Hard case (Sale) -----------------------------------------------------------------

    [Fact]
    public void HardCase_NotSoldAndNoColourFields_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(ValidSale(), Snapshot()).IsValid);
    }

    [Fact]
    public void HardCase_NotSoldButAColourWasChosen_IsRejected()
    {
        var request = ValidSale();
        request.HardCaseColourRefId = ActiveHardCaseColour;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("HardCaseSold", failure.Key);
        Assert.Equal("HardCaseColourRefId/HardCaseOtherColourText must be empty when HardCaseSold is false.", failure.Message);
    }

    [Fact]
    public void HardCase_NotSoldButFreeTextWasLeftBehind_IsRejected()
    {
        var request = ValidSale();
        request.HardCaseOtherColourText = "Olive green";

        Assert.Equal("HardCaseSold", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void HardCase_NotSoldButAnEmptyStringWasLeftInTheColourText_IsRejected()
    {
        // Same "is not null" emptiness check as the referral fields, pinned for the same reason.
        var request = ValidSale();
        request.HardCaseOtherColourText = string.Empty;

        Assert.Equal("HardCaseSold", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void HardCase_NotSoldButBothColourFieldsWereLeftBehind_IsReportedOnce()
    {
        // One flag to correct, so one message — not one per stray field.
        var request = ValidSale();
        request.HardCaseColourRefId = ActiveHardCaseColour;
        request.HardCaseOtherColourText = "Olive green";

        Assert.Equal("HardCaseSold", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void HardCase_SoldWithoutAColour_IsRejected()
    {
        var request = ValidSale();
        request.HardCaseSold = true;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("HardCaseColourRefId", failure.Key);
        Assert.Equal("HardCaseColourRefId is required when HardCaseSold is true.", failure.Message);
    }

    [Fact]
    public void HardCase_SoldWithARetiredColour_IsRejected()
    {
        var request = ValidSale();
        request.HardCaseSold = true;
        request.HardCaseColourRefId = RetiredHardCaseColour;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("HardCaseColourRefId", failure.Key);
        Assert.Equal("HardCaseColourRefId must reference an existing, active HardCaseColour reference-data item.", failure.Message);
    }

    [Fact]
    public void HardCase_SoldWithAColourFromAnotherCategory_IsRejected()
    {
        var request = ValidSale();
        request.HardCaseSold = true;
        request.HardCaseColourRefId = ActiveFrameColour;

        Assert.Equal("HardCaseColourRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void HardCase_SoldWithOtherColourButNoFreeText_IsRejected()
    {
        var request = ValidSale();
        request.HardCaseSold = true;
        request.HardCaseColourRefId = OtherHardCaseColour;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("HardCaseOtherColourText", failure.Key);
        Assert.Equal("HardCaseOtherColourText is required when HardCaseColour is \"Other\".", failure.Message);
    }

    [Fact]
    public void HardCase_SoldWithOtherColourAndFreeText_IsAccepted()
    {
        var request = ValidSale();
        request.HardCaseSold = true;
        request.HardCaseColourRefId = OtherHardCaseColour;
        request.HardCaseOtherColourText = "Olive green";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void HardCase_SoldWithAnActiveColour_IsAccepted()
    {
        var request = ValidSale();
        request.HardCaseSold = true;
        request.HardCaseColourRefId = ActiveHardCaseColour;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    // --- Composition ----------------------------------------------------------------------

    [Fact]
    public void ASaleFailingSeveralTopicsAtOnce_ReportsEachAgainstItsOwnField()
    {
        // Nothing merges or swallows: four independent topics, four independent keys.
        var request = ValidSale();
        request.OccupationRefId = NeverExisted;
        request.ReferredOrTreated = true;
        request.ReferralReasonRefId = ActiveReferralReason;
        request.FrameColourRefId = RetiredFrameColour;
        request.HardCaseOtherColourText = "Olive green";

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(
            ["OccupationRefId", "ReferralLocationFreeText", "FrameColourRefId", "HardCaseSold"],
            result.Failures.Select(f => f.Key));
    }

    [Fact]
    public void AnEmptySnapshot_RejectsEveryReferenceDataAnswerRatherThanThrowing()
    {
        // A Field App that has never been online holds nothing; the rules still have to answer.
        var request = ValidSale();
        request.OccupationRefId = ActiveOccupation;

        var result = ConsultationRules.Check(request, ReferenceDataSnapshot.Empty);

        Assert.Equal(["OccupationRefId", "FrameColourRefId"], result.Failures.Select(f => f.Key));
    }
}

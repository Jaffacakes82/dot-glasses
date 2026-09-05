using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Contracts.Tests;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests;

/// <summary>
/// Every rule <see cref="ConsultationRules"/> holds — occupation, "referred or treated", reason
/// not purchased, frame colour, hard case, the whole lens range, and the Sale's Coating set and
/// the Test/Lead's Coating preference — exercised through its three entry points, never through
/// the per-topic functions behind them: those are private precisely so a test pins the behaviour
/// rather than the composition.
///
/// The snapshot is a plain literal in every case. Occupation and referral are checked on the Test
/// request and only smoke-checked on Lead/Sale, because there is one rule body behind all three
/// entry points and a third copy of each case would be testing C#'s overload resolution rather
/// than a rule. The lens range is the exception: it is checked on whichever request actually
/// carries the variant under test, because there the three genuinely differ — a Sale requires a
/// pupil distance where a Test and Lead only range-check one that was given, and all three word
/// the out-of-range bucket message differently.
///
/// Coating splits the same way, but on a sharper line: a <b>Coating set</b> exists only on a Sale
/// and a <b>Coating preference</b> only on a Test or Lead (ADR-0001's scope correction), so the
/// two groups below are checked on the requests that actually carry them and nowhere else.
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

    private static readonly Guid ActiveLensType = Guid.Parse("00000000-0000-0000-0000-00000000001a");
    private static readonly Guid RetiredLensType = Guid.Parse("00000000-0000-0000-0000-00000000001b");
    private static readonly Guid OtherLensType = Guid.Parse("00000000-0000-0000-0000-00000000001c");

    private static readonly Guid ActiveCoating = Guid.Parse("00000000-0000-0000-0000-00000000002a");
    private static readonly Guid SecondCoating = Guid.Parse("00000000-0000-0000-0000-00000000002b");
    private static readonly Guid ExcludingCoating = Guid.Parse("00000000-0000-0000-0000-00000000002c");
    private static readonly Guid RetiredCoating = Guid.Parse("00000000-0000-0000-0000-00000000002d");

    /// <summary>Active, and configured on no lens option at all — the only way to tell the
    /// availability rule apart from the active-item rule that runs just before it.</summary>
    private static readonly Guid UnavailableCoating = Guid.Parse("00000000-0000-0000-0000-00000000002e");

    private static readonly Guid CatalogueA = Guid.Parse("00000000-0000-0000-0000-000000000f01");
    private static readonly Guid CatalogueB = Guid.Parse("00000000-0000-0000-0000-000000000f02");
    private static readonly Guid LensA1 = Guid.Parse("00000000-0000-0000-0000-000000000f11");
    private static readonly Guid LensA2 = Guid.Parse("00000000-0000-0000-0000-000000000f12");

    /// <summary>On CatalogueA like the other two, but with no Coatings configured for its
    /// strength — the state 12 of the 16 seeded LensStrength items are actually in (see
    /// <c>docs/open-issues.md</c>), and what the lens-keyed failure exists for.</summary>
    private static readonly Guid LensA3NoCoatings = Guid.Parse("00000000-0000-0000-0000-000000000f13");
    private static readonly Guid LensB1 = Guid.Parse("00000000-0000-0000-0000-000000000f21");

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

            new ReferenceItemSnapshot(ActiveLensType, ReferenceDataCategory.LensType, "Bifocal", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(RetiredLensType, ReferenceDataCategory.LensType, "Trifocal", IsActive: false, IsOtherOption: false),
            new ReferenceItemSnapshot(OtherLensType, ReferenceDataCategory.LensType, "Other", IsActive: true, IsOtherOption: true),

            // Coating has no "Other" option — it is a multi-select on a Sale, so there is no
            // single dropdown for an "Other" row to sit in.
            new ReferenceItemSnapshot(ActiveCoating, ReferenceDataCategory.Coating, "Photochromic", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(SecondCoating, ReferenceDataCategory.Coating, "Blue Block", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(ExcludingCoating, ReferenceDataCategory.Coating, "Clear", IsActive: true, IsOtherOption: false),
            new ReferenceItemSnapshot(RetiredCoating, ReferenceDataCategory.Coating, "Anti-glare", IsActive: false, IsOtherOption: false),
            new ReferenceItemSnapshot(UnavailableCoating, ReferenceDataCategory.Coating, "Sunglasses", IsActive: true, IsOtherOption: false),
        ],
        [
            // Two catalogues, so "this lens option belongs to some catalogue, just not that one"
            // is a case the tests can actually state — it is the mistake the rule exists to catch,
            // and an id that belongs to nothing would not distinguish the rule from a null check.
            //
            // UnavailableCoating is deliberately on no lens option's roster, and LensA3NoCoatings
            // deliberately has an empty one: those are the two different ways availability fails,
            // and they are reported against different fields.
            new PresetCatalogueSnapshot(CatalogueA, "Six lens set", PresetCatalogueKind.SixLensSet, [
                new LensOptionSnapshot(LensA1, "+1.00", 0, [ActiveCoating, SecondCoating, ExcludingCoating]),
                new LensOptionSnapshot(LensA2, "+2.50", 1, [ActiveCoating, SecondCoating, ExcludingCoating]),
                new LensOptionSnapshot(LensA3NoCoatings, "+3.50", 2, []),
            ]),
            new PresetCatalogueSnapshot(CatalogueB, "Nine lens set", PresetCatalogueKind.NineLensSet, [
                new LensOptionSnapshot(LensB1, "+3.00", 0, [ActiveCoating]),
            ]),
        ],
        [],
        [
            // Clear excludes Photochromic, the worked example in CONTEXT.md and ADR-0001. Stated
            // one way round only — the rule is symmetric and the snapshot canonicalizes it, which
            // is exactly what the exclusion tests below check.
            new CoatingExclusionRule(ExcludingCoating, ActiveCoating),
        ]);

    /// <summary>A request nothing objects to. On a Test or Lead that means no lens range at all —
    /// LensRangeType is nullable there and "not chosen yet" is a valid consultation. Coating
    /// preference stays null throughout (ticket 11).</summary>
    private static CreateTestRequest ValidTest() => new() { Id = Guid.NewGuid() };

    private static CreateLeadRequest ValidLead() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        PhoneNumber = "+254700000000",
        ReasonNotPurchasedRefId = ActiveReasonNotPurchased,
    };

    /// <summary>A Sale cannot decline to name a lens range: LensRangeType is non-nullable and its
    /// default is SixLensSet, so the baseline request has to carry a complete preset range —
    /// catalogue, both lens options, and the pupil-distance bucket a Sale is required to have. It
    /// also has to carry a Coating set: at least one entry is required on both branches, so a
    /// baseline with an empty one would not be valid.</summary>
    private static CreateSaleRequest ValidSale() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        FrameColourRefId = ActiveFrameColour,
        LensRangeType = LensRangeType.SixLensSet,
        PresetCatalogueId = CatalogueA,
        LensOptionLeftId = LensA1,
        LensOptionRightId = LensA2,
        PresetPupilDistanceBucket = 2,
        CoatingRefIds = [ActiveCoating],
    };

    /// <summary>A Test on a complete preset range. The bucket is left unset: optional on a Test.</summary>
    private static CreateTestRequest PresetTest() => new()
    {
        Id = Guid.NewGuid(),
        LensRangeType = LensRangeType.SixLensSet,
        PresetCatalogueId = CatalogueA,
        LensOptionLeftId = LensA1,
        LensOptionRightId = LensA2,
    };

    private static CreateLeadRequest PresetLead()
    {
        var request = ValidLead();
        request.LensRangeType = LensRangeType.SixLensSet;
        request.PresetCatalogueId = CatalogueA;
        request.LensOptionLeftId = LensA1;
        request.LensOptionRightId = LensA2;
        return request;
    }

    /// <summary>A Test on a Custom prescription: both spheres, which is the minimum a Custom
    /// branch accepts. Pupil distance is optional on a Test, so it stays unset.</summary>
    private static CreateTestRequest CustomTest() => new()
    {
        Id = Guid.NewGuid(),
        LensRangeType = LensRangeType.Custom,
        CustomSphereLeft = 1.00m,
        CustomSphereRight = -0.50m,
    };

    private static CreateLeadRequest CustomLead()
    {
        var request = ValidLead();
        request.LensRangeType = LensRangeType.Custom;
        request.CustomSphereLeft = 1.00m;
        request.CustomSphereRight = -0.50m;
        return request;
    }

    /// <summary>A Sale on a Custom prescription. Unlike the Test and Lead it must carry a pupil
    /// distance — the order cannot be ground without one.</summary>
    private static CreateSaleRequest CustomSale()
    {
        var request = ValidSale();
        request.LensRangeType = LensRangeType.Custom;
        request.PresetCatalogueId = null;
        request.LensOptionLeftId = null;
        request.LensOptionRightId = null;
        request.PresetPupilDistanceBucket = null;
        request.CustomSphereLeft = 1.00m;
        request.CustomSphereRight = -0.50m;
        request.PupilDistanceMm = 62m;
        return request;
    }

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

    // --- Lens range: choosing a branch ----------------------------------------------------

    [Fact]
    public void LensRange_NotChosenOnATest_IsAccepted()
    {
        // Nullable on a Test and a Lead: a consultation may record an outcome and stop there.
        Assert.True(ConsultationRules.Check(ValidTest(), Snapshot()).IsValid);
    }

    [Theory]
    [InlineData("preset")]
    [InlineData("custom")]
    [InlineData("pupilDistance")]
    [InlineData("bucket")]
    public void LensRange_NotChosenButALensFieldWasFilled_IsRejected(string field)
    {
        var request = ValidTest();
        switch (field)
        {
            case "preset": request.PresetCatalogueId = CatalogueA; break;
            case "custom": request.CustomSphereLeft = 1.00m; break;
            case "pupilDistance": request.PupilDistanceMm = 62m; break;
            case "bucket": request.PresetPupilDistanceBucket = 2; break;
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensRangeType", failure.Key);
        Assert.Equal("Preset/custom lens fields must be empty when LensRangeType is not set.", failure.Message);
    }

    [Fact]
    public void LensRange_PresetChosenButACustomFieldWasFilled_IsRejected()
    {
        // Fields belonging to the branch not chosen must be empty — a half-edited form must not
        // reach the database carrying two contradictory prescriptions.
        var request = PresetTest();
        request.CustomSphereLeft = 1.00m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensRangeType", failure.Key);
        Assert.Equal("Custom prescription fields must be empty for a preset LensRangeType.", failure.Message);
    }

    [Fact]
    public void LensRange_CustomChosenButAPresetFieldWasFilled_IsRejected()
    {
        var request = CustomTest();
        request.PresetCatalogueId = CatalogueA;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensRangeType", failure.Key);
        Assert.Equal("Preset fields must be empty for a Custom LensRangeType.", failure.Message);
    }

    // --- Lens range: the preset branch ----------------------------------------------------

    [Fact]
    public void Preset_CompleteAndConsistent_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(PresetTest(), Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(PresetLead(), Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(ValidSale(), Snapshot()).IsValid);
    }

    [Theory]
    [InlineData("catalogue")]
    [InlineData("left")]
    [InlineData("right")]
    public void Preset_MissingOneOfTheThreeIds_ReportsOnceAndStops(string missing)
    {
        // Without all three there is nothing to check the options against, so the branch reports
        // the one thing the technician can act on and stops — continuing would complain that an id
        // they never supplied does not belong to a catalogue.
        var request = PresetTest();
        switch (missing)
        {
            case "catalogue": request.PresetCatalogueId = null; break;
            case "left": request.LensOptionLeftId = null; break;
            case "right": request.LensOptionRightId = null; break;
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("PresetCatalogueId", failure.Key);
        Assert.Equal("PresetCatalogueId, LensOptionLeftId and LensOptionRightId are all required for a preset LensRangeType.", failure.Message);
    }

    [Fact]
    public void Preset_MissingIdsStopsBeforeThePupilDistanceChecks()
    {
        // The same short-circuit, stated as the thing that actually matters: a Sale with no
        // catalogue reports the missing ids alone, not that plus a required-bucket message.
        var request = ValidSale();
        request.PresetCatalogueId = null;
        request.PresetPupilDistanceBucket = null;

        Assert.Equal("PresetCatalogueId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Preset_LensOptionFromAnotherCatalogue_IsRejected()
    {
        // LensB1 is a real lens option — it just belongs to the other catalogue. That is the
        // mistake this rule exists to catch, and an id belonging to nothing would not tell the
        // rule apart from a null check.
        var request = PresetTest();
        request.LensOptionLeftId = LensB1;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensOptionLeftId", failure.Key);
        Assert.Equal("LensOptionLeftId must belong to PresetCatalogueId.", failure.Message);
    }

    [Fact]
    public void Preset_BothLensOptionsFromAnotherCatalogue_AreReportedSeparately()
    {
        var request = PresetTest();
        request.LensOptionLeftId = LensB1;
        request.LensOptionRightId = LensB1;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(["LensOptionLeftId", "LensOptionRightId"], result.Failures.Select(f => f.Key));
        Assert.Equal("LensOptionRightId must belong to PresetCatalogueId.", result.Failures[1].Message);
    }

    [Fact]
    public void Preset_LensOptionThatNeverExisted_IsRejected()
    {
        var request = PresetTest();
        request.LensOptionRightId = NeverExisted;

        Assert.Equal("LensOptionRightId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void Preset_MillimetrePupilDistance_IsRejected()
    {
        // A preset range records the pupil distance as a coarse bucket; a millimetre reading here
        // means the technician filled the wrong control.
        var request = PresetTest();
        request.PupilDistanceMm = 62m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("PupilDistanceMm", failure.Key);
        Assert.Equal("PupilDistanceMm must be empty for a preset LensRangeType — use PresetPupilDistanceBucket instead.", failure.Message);
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(4, false, true)]   // top of the adult range
    [InlineData(5, false, false)]  // one past it
    [InlineData(-1, false, false)]
    [InlineData(2, true, true)]    // top of the children's range
    [InlineData(3, true, false)]   // in range for an adult frame, out of it for a child's
    public void Preset_PupilDistanceBucketBoundaries(int bucket, bool childrensFrame, bool accepted)
    {
        var request = PresetTest();
        request.PresetPupilDistanceBucket = bucket;
        request.ChildrensFrame = childrensFrame;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            Assert.Equal("PresetPupilDistanceBucket", Assert.Single(result.Failures).Key);
        }
    }

    [Fact]
    public void Preset_PupilDistanceBucketOmitted_IsAcceptedOnATestAndLeadButNotOnASale()
    {
        // The one rule that genuinely differs by request type: a Sale's order cannot be ground
        // without a pupil distance, while a Test or Lead is often taken at a busy event with no
        // time to measure one.
        var test = PresetTest();
        var lead = PresetLead();
        var sale = ValidSale();
        sale.PresetPupilDistanceBucket = null;

        Assert.True(ConsultationRules.Check(test, Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(lead, Snapshot()).IsValid);
        Assert.Equal("PresetPupilDistanceBucket", AssertSingleFailure(ConsultationRules.Check(sale, Snapshot())).Key);
    }

    [Fact]
    public void Preset_OutOfRangeBucketKeepsEachRequestTypesOwnWording()
    {
        // Pre-existing copy drift, pinned rather than harmonised: the rule is identical on all
        // three, only the sentence differs. A test is the only thing stopping a future tidy-up
        // from silently rewording copy a technician reads.
        var test = PresetTest();
        test.PresetPupilDistanceBucket = 9;
        var lead = PresetLead();
        lead.PresetPupilDistanceBucket = 9;
        var sale = ValidSale();
        sale.PresetPupilDistanceBucket = 9;

        Assert.Equal(
            "PresetPupilDistanceBucket must be between 0 and 4.",
            AssertSingleFailure(ConsultationRules.Check(test, Snapshot())).Message);
        Assert.Equal(
            "PresetPupilDistanceBucket must be between 0 and 4 for a preset LensRangeType.",
            AssertSingleFailure(ConsultationRules.Check(lead, Snapshot())).Message);
        Assert.Equal(
            "PresetPupilDistanceBucket is required and must be between 0 and 4 for a preset LensRangeType.",
            AssertSingleFailure(ConsultationRules.Check(sale, Snapshot())).Message);
    }

    [Fact]
    public void Preset_ChildrensFrameNamesItsLowerCeilingInTheMessage()
    {
        var lead = PresetLead();
        lead.ChildrensFrame = true;
        lead.PresetPupilDistanceBucket = 3;
        var sale = ValidSale();
        sale.ChildrensFrame = true;
        sale.PresetPupilDistanceBucket = null;

        Assert.Equal(
            "PresetPupilDistanceBucket must be between 0 and 2 for a preset LensRangeType (0-2 for a children's frame).",
            AssertSingleFailure(ConsultationRules.Check(lead, Snapshot())).Message);
        Assert.Equal(
            "PresetPupilDistanceBucket is required and must be between 0 and 2 for a preset LensRangeType (0-2 for a children's frame).",
            AssertSingleFailure(ConsultationRules.Check(sale, Snapshot())).Message);
    }

    // --- Lens range: the Custom branch ----------------------------------------------------

    [Fact]
    public void Custom_BothSpheres_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(CustomTest(), Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(CustomLead(), Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(CustomSale(), Snapshot()).IsValid);
    }

    [Theory]
    [InlineData("left")]
    [InlineData("right")]
    public void Custom_MissingASphere_IsRejected(string missing)
    {
        // One eye's prescription is not a prescription. Note the failure reports against
        // LensRangeType rather than the sphere field: the branch as a whole is incomplete.
        var request = CustomTest();
        if (missing == "left")
        {
            request.CustomSphereLeft = null;
        }
        else
        {
            request.CustomSphereRight = null;
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensRangeType", failure.Key);
        Assert.Equal("CustomSphereLeft and CustomSphereRight are required for a Custom LensRangeType.", failure.Message);
    }

    [Theory]
    [InlineData(-10, true)]      // bottom of the ground range
    [InlineData(10, true)]       // top of it
    [InlineData(0.25, true)]
    [InlineData(-10.25, false)]  // on the quarter-dioptre step, but below the range
    [InlineData(10.25, false)]   // on the step, above the range
    [InlineData(0.30, false)]    // inside the range, off the step — the increment rule alone
    [InlineData(2.1, false)]
    public void Custom_SpherePowerBoundariesAndIncrement(decimal sphere, bool accepted)
    {
        // Range and increment are one question with one message: a power inside the range but off
        // the quarter-dioptre step is no more grindable than one outside it.
        var request = CustomTest();
        request.CustomSphereLeft = sphere;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            Assert.Equal("CustomSphereLeft", Assert.Single(result.Failures).Key);
        }
    }

    [Fact]
    public void Custom_OffStepPowerNamesTheRangeAndTheStep()
    {
        var request = CustomTest();
        request.CustomSphereLeft = 0.30m;

        Assert.Equal(
            "CustomSphereLeft must be between -10 and 10 in 0.25 increments.",
            AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Message);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]      // top of the add-power range, narrower than a sphere's
    [InlineData(3.25, false)]
    [InlineData(-0.25, false)]
    [InlineData(0.1, false)]   // off the step
    public void Custom_AddPowerHasItsOwnNarrowerRange(decimal addPower, bool accepted)
    {
        // A lens type is set alongside, because an add power is exactly what makes one required —
        // this case is about the power's range, not that requirement.
        var request = CustomTest();
        request.CustomAddPowerLeft = addPower;
        request.LensTypeRefId = ActiveLensType;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            var failure = Assert.Single(result.Failures);
            Assert.Equal("CustomAddPowerLeft", failure.Key);
            Assert.Equal("CustomAddPowerLeft must be between 0 and 3 in 0.25 increments.", failure.Message);
        }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(180, true)]   // a bearing of 180 degrees is in range
    [InlineData(181, false)]
    [InlineData(-1, false)]
    [InlineData(90.5, false)] // whole degrees only
    public void Custom_AxisBoundaries(decimal axis, bool accepted)
    {
        var request = CustomTest();
        request.CustomAxisLeft = axis;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            var failure = Assert.Single(result.Failures);
            Assert.Equal("CustomAxisLeft", failure.Key);
            Assert.Equal("CustomAxisLeft must be a whole number of degrees between 0 and 180.", failure.Message);
        }
    }

    [Fact]
    public void Custom_BothEyesPowersAreCheckedIndependently()
    {
        var request = CustomTest();
        request.CustomSphereLeft = 0.30m;
        request.CustomCylinderRight = -20m;
        request.CustomAxisRight = 200m;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(["CustomSphereLeft", "CustomCylinderRight", "CustomAxisRight"], result.Failures.Select(f => f.Key));
    }

    // --- Lens range: the lens type ---------------------------------------------------------

    [Fact]
    public void LensType_RequiredOnceAnEyeCarriesTwoDistinctPowers()
    {
        var request = CustomTest();
        request.CustomAddPowerLeft = 2.00m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("LensTypeRefId is required when an add power is set (two distinct powers on that eye).", failure.Message);
    }

    [Fact]
    public void LensType_TheOtherEyesAddPowerTriggersItToo()
    {
        var request = CustomTest();
        request.CustomAddPowerRight = 2.00m;

        Assert.Equal("LensTypeRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Theory]
    [InlineData("refId")]
    [InlineData("otherText")]
    public void LensType_SetWithoutAnAddPower_IsRejected(string field)
    {
        // Exactly when, not merely if: with a single power there is no bifocal to name, so both
        // lens-type fields must stay empty.
        var request = CustomTest();
        if (field == "refId")
        {
            request.LensTypeRefId = ActiveLensType;
        }
        else
        {
            request.LensTypeOtherText = "Progressive";
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("LensTypeRefId/LensTypeOtherText must be empty unless an add power is set.", failure.Message);
    }

    [Fact]
    public void LensType_RetiredItem_IsRejected()
    {
        var request = CustomTest();
        request.CustomAddPowerLeft = 2.00m;
        request.LensTypeRefId = RetiredLensType;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("LensTypeRefId must reference an existing, active LensType reference-data item.", failure.Message);
    }

    [Fact]
    public void LensType_ItemFromAnotherCategory_IsRejected()
    {
        var request = CustomTest();
        request.CustomAddPowerLeft = 2.00m;
        request.LensTypeRefId = ActiveOccupation;

        Assert.Equal("LensTypeRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void LensType_OtherWithoutFreeText_IsRejected()
    {
        var request = CustomTest();
        request.CustomAddPowerLeft = 2.00m;
        request.LensTypeRefId = OtherLensType;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeOtherText", failure.Key);
        Assert.Equal("LensTypeOtherText is required when LensType is \"Other\".", failure.Message);
    }

    [Fact]
    public void LensType_OtherWithFreeText_IsAccepted()
    {
        var request = CustomTest();
        request.CustomAddPowerLeft = 2.00m;
        request.LensTypeRefId = OtherLensType;
        request.LensTypeOtherText = "Progressive";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    // --- Lens range: pupil distance on the Custom branch -----------------------------------

    [Fact]
    public void Custom_PresetBucket_IsRejected()
    {
        var request = CustomTest();
        request.PresetPupilDistanceBucket = 2;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("PresetPupilDistanceBucket", failure.Key);
        Assert.Equal("PresetPupilDistanceBucket must be empty for a Custom LensRangeType — use PupilDistanceMm instead.", failure.Message);
    }

    [Theory]
    [InlineData(54, true)]    // bottom of the sellable range
    [InlineData(74, true)]    // top of it
    [InlineData(53, false)]
    [InlineData(75, false)]
    [InlineData(60.5, false)] // in range, but not a whole millimetre
    public void Custom_PupilDistanceBoundaries(decimal pupilDistance, bool accepted)
    {
        var request = CustomTest();
        request.PupilDistanceMm = pupilDistance;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            Assert.Equal("PupilDistanceMm", Assert.Single(result.Failures).Key);
        }
    }

    [Fact]
    public void Custom_OutOfRangeAndNonWholePupilDistanceAreDifferentMessages()
    {
        // Only ever one at a time: a technician correcting 53.5 has one thing to fix, not two.
        var outOfRange = CustomTest();
        outOfRange.PupilDistanceMm = 53.5m;
        var nonWhole = CustomTest();
        nonWhole.PupilDistanceMm = 60.5m;

        Assert.Equal(
            "PupilDistanceMm must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).",
            AssertSingleFailure(ConsultationRules.Check(outOfRange, Snapshot())).Message);
        Assert.Equal(
            "PupilDistanceMm must be a whole millimetre value.",
            AssertSingleFailure(ConsultationRules.Check(nonWhole, Snapshot())).Message);
    }

    [Fact]
    public void Custom_PupilDistanceOmitted_IsAcceptedOnATestAndLeadButNotOnASale()
    {
        var sale = CustomSale();
        sale.PupilDistanceMm = null;

        Assert.True(ConsultationRules.Check(CustomTest(), Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(CustomLead(), Snapshot()).IsValid);

        var failure = AssertSingleFailure(ConsultationRules.Check(sale, Snapshot()));

        Assert.Equal("PupilDistanceMm", failure.Key);
        Assert.Equal(
            "PupilDistanceMm is required and must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).",
            failure.Message);
    }

    [Fact]
    public void Custom_OutOfRangePupilDistanceOnASaleSaysItIsRequiredToo()
    {
        // The Sale's range message is the required-variant whether the value is missing or merely
        // out of range — one sentence covers both, exactly as it did before the move.
        var sale = CustomSale();
        sale.PupilDistanceMm = 80m;

        Assert.Equal(
            "PupilDistanceMm is required and must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).",
            AssertSingleFailure(ConsultationRules.Check(sale, Snapshot())).Message);
    }

    // --- Coating set (Sale) ---------------------------------------------------------------

    [Fact]
    public void CoatingSet_OneAvailableActiveCoating_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(ValidSale(), Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingSet_SeveralAvailableActiveCoatings_IsAccepted()
    {
        // A set, not a single value — the whole point of ADR-0001.
        var request = ValidSale();
        request.CoatingRefIds = [ActiveCoating, SecondCoating];

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CoatingSet_Empty_IsRejectedOnBothBranches(bool preset)
    {
        // Required on a preset range and on a Custom prescription alike: a sold lens always
        // carries at least one Coating.
        var request = preset ? ValidSale() : CustomSale();
        request.CoatingRefIds = [];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("Choose at least one coating.", failure.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CoatingSet_ContainingTheSameCoatingTwice_IsRejectedOnBothBranches(bool preset)
    {
        var request = preset ? ValidSale() : CustomSale();
        request.CoatingRefIds = [ActiveCoating, ActiveCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("CoatingRefIds must not contain duplicates.", failure.Message);
    }

    [Fact]
    public void CoatingSet_ContainingARetiredCoating_IsRejected()
    {
        var request = ValidSale();
        request.CoatingRefIds = [ActiveCoating, RetiredCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("CoatingRefIds must only reference existing, active Coating reference-data items.", failure.Message);
    }

    [Fact]
    public void CoatingSet_ContainingAnItemFromAnotherCategory_IsRejected()
    {
        // A Guid that resolves to a Frame colour is not an answer to "which Coating is this".
        var request = ValidSale();
        request.CoatingRefIds = [ActiveFrameColour];

        Assert.Equal(
            "CoatingRefIds must only reference existing, active Coating reference-data items.",
            AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Message);
    }

    [Fact]
    public void CoatingSet_OnAPresetRange_RejectsACoatingNotConfiguredForTheChosenLens()
    {
        // UnavailableCoating is active and real — it is simply not on this lens strength's
        // roster. That is the distinction between this rule and the active-item check above.
        var request = ValidSale();
        request.CoatingRefIds = [UnavailableCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("Every coating must be configured as available for the chosen lens option (see Reference Data > Lens Strength).", failure.Message);
    }

    [Fact]
    public void CoatingSet_OnACustomPrescription_AcceptsAnyActiveCoatingRegardlessOfLensAvailability()
    {
        // Availability is a per-catalogue restriction, so a Custom prescription — which names no
        // catalogue at all — has nothing to restrict against.
        var request = CustomSale();
        request.CoatingRefIds = [UnavailableCoating];

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingSet_AvailabilityIsScopedByTheLeftLensOnly()
    {
        // The rule reads the left lens option and only the left one. LensB1 carries a different
        // roster, and putting it on the right does not widen or narrow what the left allows —
        // it is rejected for belonging to another catalogue, and the coatings still pass.
        var request = ValidSale();
        request.LensOptionRightId = LensB1;
        request.CoatingRefIds = [SecondCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensOptionRightId", failure.Key);
    }

    [Fact]
    public void CoatingSet_OnALensWithNoCoatingsConfigured_IsReportedAgainstTheLensNotTheSet()
    {
        // The behaviour change ticket 11 made deliberately. IsCoatingAvailableForLensOption
        // returns false rather than throwing when a strength has no coatings configured, so this
        // used to read "every coating must be configured as available for the chosen lens option"
        // against CoatingRefIds — advice no choice of coating could satisfy, because none is
        // available. It is the lens that has to change.
        var request = ValidSale();
        request.LensOptionLeftId = LensA3NoCoatings;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensOptionLeftId", failure.Key);
        Assert.Equal("This lens has no coatings configured yet, so it can't be sold on a preset range.", failure.Message);
    }

    [Fact]
    public void CoatingSet_OnALensWithNoCoatingsConfigured_SaysSoEvenWhenNoCoatingWasChosen()
    {
        // Asked ahead of "choose at least one coating": sending the technician to a picker with
        // nothing in it would be the one piece of advice they cannot act on.
        var request = ValidSale();
        request.LensOptionLeftId = LensA3NoCoatings;
        request.CoatingRefIds = [];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensOptionLeftId", failure.Key);
        Assert.Equal("This lens has no coatings configured yet, so it can't be sold on a preset range.", failure.Message);
    }

    [Fact]
    public void CoatingSet_TwoCoatingsThatExcludeOneAnother_IsRejected()
    {
        var request = ValidSale();
        request.CoatingRefIds = [ExcludingCoating, ActiveCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("This coating combination isn't allowed — two of the selected coatings exclude each other.", failure.Message);
    }

    [Fact]
    public void CoatingSet_ExclusionIsCheckedSymmetrically()
    {
        // The snapshot holds the pair one way round only (Clear → Photochromic). Selecting them
        // in the opposite order has to be rejected identically, or the rule would depend on which
        // checkbox the technician happened to tick first.
        var forwards = ValidSale();
        forwards.CoatingRefIds = [ExcludingCoating, ActiveCoating];
        var backwards = ValidSale();
        backwards.CoatingRefIds = [ActiveCoating, ExcludingCoating];

        Assert.Equal(
            "This coating combination isn't allowed — two of the selected coatings exclude each other.",
            AssertSingleFailure(ConsultationRules.Check(forwards, Snapshot())).Message);
        Assert.Equal(
            "This coating combination isn't allowed — two of the selected coatings exclude each other.",
            AssertSingleFailure(ConsultationRules.Check(backwards, Snapshot())).Message);
    }

    [Fact]
    public void CoatingSet_ExclusionIsCheckedAcrossEveryPairNotJustAdjacentOnes()
    {
        // Three coatings, and the excluding pair sits at either end. A rule that only compared
        // neighbours would let this through.
        var request = ValidSale();
        request.CoatingRefIds = [ExcludingCoating, SecondCoating, ActiveCoating];

        Assert.Equal(
            "This coating combination isn't allowed — two of the selected coatings exclude each other.",
            AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Message);
    }

    [Fact]
    public void CoatingSet_ExclusionAppliesOnACustomPrescriptionToo()
    {
        // Exclusion describes physical compatibility between coatings, so it holds on both
        // branches — unlike availability, which is a per-catalogue restriction (ADR-0001).
        var request = CustomSale();
        request.CoatingRefIds = [ExcludingCoating, ActiveCoating];

        Assert.Equal(
            "This coating combination isn't allowed — two of the selected coatings exclude each other.",
            AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Message);
    }

    [Fact]
    public void CoatingSet_ACoatingDoesNotExcludeItself()
    {
        // Guarded by the duplicate check before it, but worth stating: canonicalizing a pair of
        // identical ids must not make a coating exclude itself.
        var request = ValidSale();
        request.CoatingRefIds = [ExcludingCoating];

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingSet_WithoutACompletePresetRange_SaysNothing()
    {
        // No left lens option means nothing to scope availability by, and telling a technician
        // who has not picked a lens yet to choose a coating would be noise on top of the real
        // failure — the same short-circuit the lens-range rule makes.
        var request = ValidSale();
        request.LensOptionLeftId = null;
        request.CoatingRefIds = [];

        Assert.Equal(
            ["PresetCatalogueId"],
            ConsultationRules.Check(request, Snapshot()).Failures.Select(f => f.Key));
    }

    // --- Coating preference (Test/Lead) ----------------------------------------------------

    [Fact]
    public void CoatingPreference_NotRecorded_IsAccepted()
    {
        // Optional — a Test or Lead often records no preference at all.
        Assert.True(ConsultationRules.Check(ValidTest(), Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(ValidLead(), Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingPreference_ActiveCoating_IsAccepted()
    {
        var test = ValidTest();
        test.CoatingPreferenceRefId = ActiveCoating;
        var lead = ValidLead();
        lead.CoatingPreferenceRefId = ActiveCoating;

        Assert.True(ConsultationRules.Check(test, Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(lead, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingPreference_RetiredCoating_IsRejected()
    {
        var test = ValidTest();
        test.CoatingPreferenceRefId = RetiredCoating;

        var failure = AssertSingleFailure(ConsultationRules.Check(test, Snapshot()));

        Assert.Equal("CoatingPreferenceRefId", failure.Key);
        Assert.Equal("CoatingPreferenceRefId must reference an existing, active Coating reference-data item.", failure.Message);
    }

    [Fact]
    public void CoatingPreference_RecordedBeforeAnyLensWasChosen_IsAccepted()
    {
        // A preference is an intention captured before any lens exists (CONTEXT.md), so it is
        // asked for every LensRangeType including the unset one — where there is no lens to scope
        // availability by, and none is applied.
        var test = ValidTest();
        test.CoatingPreferenceRefId = UnavailableCoating;

        Assert.True(ConsultationRules.Check(test, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingPreference_OnAPresetRange_MustBeAvailableForTheChosenLens()
    {
        var test = PresetTest();
        test.CoatingPreferenceRefId = UnavailableCoating;

        var failure = AssertSingleFailure(ConsultationRules.Check(test, Snapshot()));

        Assert.Equal("CoatingPreferenceRefId", failure.Key);
        Assert.Equal("CoatingPreferenceRefId is not configured as available for the chosen lens option (see Reference Data > Lens Strength).", failure.Message);
    }

    [Fact]
    public void CoatingPreference_OnACustomPrescription_IsNotScopedByAnyLens()
    {
        var test = CustomTest();
        test.CoatingPreferenceRefId = UnavailableCoating;

        Assert.True(ConsultationRules.Check(test, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingPreference_OnALensWithNoCoatingsConfigured_StaysKeyedToThePreference()
    {
        // Deliberately *not* the lens-keyed failure the Sale's Coating set now reports. A
        // preference is optional, so a technician can always clear it and carry on — it is never
        // unsatisfiable the way a mandatory Coating set is.
        var test = PresetTest();
        test.LensOptionLeftId = LensA3NoCoatings;
        test.CoatingPreferenceRefId = ActiveCoating;

        var failure = AssertSingleFailure(ConsultationRules.Check(test, Snapshot()));

        Assert.Equal("CoatingPreferenceRefId", failure.Key);
    }

    [Fact]
    public void CoatingPreference_FailingBothChecks_KeepsEachRequestTypesOwnOrdering()
    {
        // Pre-existing ordering drift, preserved: both failures report against
        // CoatingPreferenceRefId, and a Test has always reported availability first where a Lead
        // reports the active-item check first. Harmonising it would be its own decision.
        var test = PresetTest();
        test.CoatingPreferenceRefId = RetiredCoating;
        var lead = PresetLead();
        lead.CoatingPreferenceRefId = RetiredCoating;

        var testFailures = ConsultationRules.Check(test, Snapshot()).Failures;
        var leadFailures = ConsultationRules.Check(lead, Snapshot()).Failures;

        Assert.Equal(
            ["CoatingPreferenceRefId is not configured as available for the chosen lens option (see Reference Data > Lens Strength).",
             "CoatingPreferenceRefId must reference an existing, active Coating reference-data item."],
            testFailures.Select(f => f.Message));
        Assert.Equal(
            ["CoatingPreferenceRefId must reference an existing, active Coating reference-data item.",
             "CoatingPreferenceRefId is not configured as available for the chosen lens option (see Reference Data > Lens Strength)."],
            leadFailures.Select(f => f.Message));
    }

    [Fact]
    public void CoatingPreference_IsASingleValueNotASet()
    {
        // Per ADR-0001's scope correction a Test or Lead never carries a Coating set, so none of
        // the set rules reach them: a preference that excludes nothing and duplicates nothing is
        // simply one id, checked once.
        Assert.Null(typeof(CreateTestRequest).GetProperty("CoatingRefIds"));
        Assert.Null(typeof(CreateLeadRequest).GetProperty("CoatingRefIds"));
    }

    // --- Scalars --------------------------------------------------------------------------
    //
    // These pin FluentValidation's generated copy character-for-character. The three validators
    // that used to produce it were deleted in ticket 12, so nothing but these assertions now
    // stands between a client and a silently reworded message — and the Field App renders these
    // strings verbatim against the control that produced them. Each expected string below was
    // captured from the real validators before they were deleted, not written from memory.

    [Fact]
    public void AnIdThatWasNeverFilledIn_IsRejectedInFluentValidationsWording()
    {
        var request = ValidTest();
        request.Id = Guid.Empty;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(new RuleFailure("Id", "'Id' must not be empty."), Assert.Single(result.Failures));
    }

    [Fact]
    public void AnOverLongFreeTextField_ReportsBothThePermittedAndTheActualLength()
    {
        // The spaced display name and the trailing "You entered ..." clause are FluentValidation's,
        // and the 201 is interpolated from the value rather than fixed.
        var request = ValidTest();
        request.OccupationOtherText = new string('a', 201);
        request.ReferralLocationFreeText = new string('b', 501);
        request.ReferredOrTreated = true;
        request.ReferralReasonRefId = ActiveReferralReason;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Contains(
            new RuleFailure("OccupationOtherText", "The length of 'Occupation Other Text' must be 200 characters or fewer. You entered 201 characters."),
            result.Failures);
        Assert.Contains(
            new RuleFailure("ReferralLocationFreeText", "The length of 'Referral Location Free Text' must be 500 characters or fewer. You entered 501 characters."),
            result.Failures);
    }

    [Fact]
    public void AFreeTextFieldExactlyAtItsCap_IsAccepted()
    {
        // The cap is inclusive, and null/empty are a different question this rule never asks.
        var request = ValidTest();
        request.OccupationOtherText = new string('a', 200);

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void AnEnumValueOutsideItsEnum_QuotesTheNumberBack()
    {
        // That number is the only clue to what the client actually sent, which is why the message
        // repeats it rather than just naming the field.
        var request = ValidTest();
        request.Gender = (Gender)99;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(
            new RuleFailure("Gender", "'Gender' has a range of values which does not include '99'."),
            Assert.Single(result.Failures));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public void AnImplausibleAge_IsRejectedAndQuotedBack(int ageYears)
    {
        var request = ValidTest();
        request.AgeYears = ageYears;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(
            new RuleFailure("AgeYears", $"'Age Years' must be between 0 and 120. You entered {ageYears}."),
            Assert.Single(result.Failures));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(120)]
    public void AnAgeThatIsAbsentOrOnTheBoundary_IsAccepted(int? ageYears)
    {
        // Absent is valid on all three requests — an age is optional — and both ends are inclusive.
        var request = ValidTest();
        request.AgeYears = ageYears;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ALeadWithNoUsableCustomerName_IsRejected(string fullName)
    {
        // Whitespace counts as empty, matching the FluentValidation rule this replaced: a customer
        // named " " is not a named customer.
        var request = ValidLead();
        request.FullName = fullName;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(
            new RuleFailure("FullName", "'Full Name' must not be empty."),
            Assert.Single(result.Failures));
    }

    [Fact]
    public void ASalesOrderFlaggedForDotGlassesOnAPresetRange_IsRejected()
    {
        // The one scalar carrying hand-written copy rather than FluentValidation's: "must be equal
        // to False" says nothing a technician could act on.
        var request = ValidSale();
        request.OrderFromDotGlasses = true;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(
            new RuleFailure("OrderFromDotGlasses", "OrderFromDotGlasses is only meaningful when LensRangeType is Custom."),
            Assert.Single(result.Failures));
    }

    [Fact]
    public void ASalesOrderFlaggedForDotGlassesOnACustomPrescription_IsAccepted()
    {
        var request = CustomSale();
        request.OrderFromDotGlasses = true;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void OnlyATestCapsItsLensTypeOtherText_AndOnlyASaleRangeChecksItsLensRangeType()
    {
        // Two pieces of pre-existing drift, preserved rather than tidied when the scalars moved
        // here (ticket 12). A Test length-caps LensTypeOtherText and a Lead never has; a Sale
        // range-checks LensRangeType and a Lead never has, though it carries the same enum. Pinned
        // so that harmonising either becomes a deliberate decision rather than an accident.
        var test = CustomTest();
        test.CustomAddPowerLeft = 1.00m;
        test.LensTypeRefId = ActiveLensType;
        test.LensTypeOtherText = new string('a', 201);

        var lead = CustomLead();
        lead.CustomAddPowerLeft = 1.00m;
        lead.LensTypeRefId = ActiveLensType;
        lead.LensTypeOtherText = new string('a', 201);

        Assert.Equal(
            new RuleFailure("LensTypeOtherText", "The length of 'Lens Type Other Text' must be 200 characters or fewer. You entered 201 characters."),
            Assert.Single(ConsultationRules.Check(test, Snapshot()).Failures));
        Assert.True(ConsultationRules.Check(lead, Snapshot()).IsValid);

        var outOfEnumLead = ValidLead();
        outOfEnumLead.LensRangeType = (LensRangeType)99;
        var outOfEnumSale = ValidSale();
        outOfEnumSale.LensRangeType = (LensRangeType)99;

        Assert.True(ConsultationRules.Check(outOfEnumLead, Snapshot()).IsValid);
        Assert.Contains(
            new RuleFailure("LensRangeType", "'Lens Range Type' has a range of values which does not include '99'."),
            ConsultationRules.Check(outOfEnumSale, Snapshot()).Failures);
    }

    [Fact]
    public void ScalarFailures_AreReportedAheadOfTheReferenceDataTopics()
    {
        // Declaration order on the deleted validators put the RuleFor chain before the module call,
        // so a request failing both reported its scalars first. Clients group by key rather than
        // reading the list in order, but LeadConversionController replays the sequence into
        // ModelState, so it stays worth pinning.
        var request = ValidSale();
        request.Id = Guid.Empty;
        request.FrameColourRefId = RetiredFrameColour;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(["Id", "FrameColourRefId"], result.Failures.Select(f => f.Key));
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
        // A Field App that has never been online holds nothing — no reference items and no preset
        // catalogues — and the rules still have to answer rather than throw. The lens options and
        // the Coating set are rejected for the same reason the occupation is: nothing in the
        // snapshot carries that id.
        //
        // The Coating set reports "not an active Coating" rather than the lens-keyed
        // no-coatings-configured message, and that is the intended split: with an empty snapshot
        // the left lens option resolves to nothing at all, which is a different failure that the
        // lens-range rule has already reported against LensOptionLeftId.
        var request = ValidSale();
        request.OccupationRefId = ActiveOccupation;

        var result = ConsultationRules.Check(request, ReferenceDataSnapshot.Empty);

        Assert.Equal(
            ["OccupationRefId", "FrameColourRefId", "LensOptionLeftId", "LensOptionRightId", "CoatingRefIds"],
            result.Failures.Select(f => f.Key));
    }
}

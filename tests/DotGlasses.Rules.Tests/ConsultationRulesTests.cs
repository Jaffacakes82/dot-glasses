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

    /// <summary>On CatalogueA like the other two, but with no Coatings of its own — a lens set lens
    /// is meant to carry at least one (ADR-0007), and this is what the lens-keyed failure exists
    /// for when one doesn't.</summary>
    private static readonly Guid LensA3NoCoatings = Guid.Parse("00000000-0000-0000-0000-000000000f13");

    /// <summary>A bifocal on CatalogueA sharing LensA1's sphere (+1.00) — what a mixed pair is
    /// made of, and the lens a request's lens type has to agree with.</summary>
    private static readonly Guid LensA4Bifocal = Guid.Parse("00000000-0000-0000-0000-000000000f14");

    /// <summary>Single vision on CatalogueA in fewer coatings than LensA1 — only ActiveCoating —
    /// so a right eye can narrow nothing the left lens allows.</summary>
    private static readonly Guid LensA5 = Guid.Parse("00000000-0000-0000-0000-000000000f15");

    /// <summary>Single vision +0.50 on CatalogueA in all three of LensA1's coatings, carrying the
    /// one pairing in this snapshot: Blue Block (SecondCoating) → Photochromic (ActiveCoating).</summary>
    private static readonly Guid LensA6Paired = Guid.Parse("00000000-0000-0000-0000-000000000f16");

    /// <summary>Single vision +0.25 on CatalogueA in Blue Block and Clear but not Photochromic — so
    /// next to LensA6Paired, Blue Block is in both lenses but its paired coating isn't.</summary>
    private static readonly Guid LensA7NoPhotochromic = Guid.Parse("00000000-0000-0000-0000-000000000f17");
    private static readonly Guid LensB1 = Guid.Parse("00000000-0000-0000-0000-000000000f21");

    /// <summary>A retired lens set: present in the server's snapshot (historical records still
    /// resolve it) but no longer sellable.</summary>
    private static readonly Guid RetiredCatalogue = Guid.Parse("00000000-0000-0000-0000-000000000f03");
    private static readonly Guid LensRetired1 = Guid.Parse("00000000-0000-0000-0000-000000000f31");

    private static readonly Guid NeverExisted = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    /// <summary>The server's filling: the whole library, retired items carrying IsActive = false.
    /// Every category this batch touches has an active item, a retired one, and the category's one
    /// "Other" option.
    ///
    /// Lens sets are left without assignment paths — the device's filling, already narrowed to its
    /// retail point — unless <paramref name="catalogueAAssignedTo"/> gives CatalogueA the server's
    /// kind: the org paths it is assigned to, checked against the record's location.</summary>
    private static ReferenceDataSnapshot Snapshot(IReadOnlyList<string>? catalogueAAssignedTo = null) => new(
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
            // Two catalogues, so "this lens power is in some lens set, just not that one" (+3.00,
            // LensB1's) is a case the tests can actually state — a record holds powers, not lens
            // ids (ADR-0007), and each eye is matched in the chosen set only.
            //
            // UnavailableCoating is deliberately on no lens option's roster, and LensA3NoCoatings
            // deliberately has an empty one: those are the two different ways availability fails,
            // and they are reported against different fields.
            new PresetCatalogueSnapshot(CatalogueA, "Six lens set", IsActive: true, [
                new LensOptionSnapshot(LensA1, "+1.00", 1.00m, [ActiveCoating, SecondCoating, ExcludingCoating]),
                new LensOptionSnapshot(LensA2, "+2.50", 2.50m, [ActiveCoating, SecondCoating, ExcludingCoating]),
                new LensOptionSnapshot(LensA3NoCoatings, "+3.50", 3.50m, []),
                new LensOptionSnapshot(LensA4Bifocal, "Bifocal +1.00 / +2.00", 1.00m, [ActiveCoating], Add: 2.00m, LensTypeRefId: ActiveLensType),
                new LensOptionSnapshot(LensA5, "+2.00", 2.00m, [ActiveCoating]),
                new LensOptionSnapshot(LensA6Paired, "+0.50", 0.50m, [ActiveCoating, SecondCoating, ExcludingCoating],
                    Pairings: [new CoatingPairingRule(SecondCoating, ActiveCoating)]),
                new LensOptionSnapshot(LensA7NoPhotochromic, "+0.25", 0.25m, [SecondCoating, ExcludingCoating]),
            ], AssignedOrgPaths: catalogueAAssignedTo),
            new PresetCatalogueSnapshot(CatalogueB, "Nine lens set", IsActive: true, [
                new LensOptionSnapshot(LensB1, "+3.00", 3.00m, [ActiveCoating]),
            ], AssignedOrgPaths: null),
            new PresetCatalogueSnapshot(RetiredCatalogue, "Retired lens set", IsActive: false, [
                new LensOptionSnapshot(LensRetired1, "+1.50", 1.50m, [ActiveCoating]),
            ], AssignedOrgPaths: null),
        ],
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
        CustomerToldPrice = true,
    };

    /// <summary>A Sale cannot decline to name a lens range: LensRangeType is non-nullable and its
    /// default is LensSet, so the baseline request has to carry a complete lens set range —
    /// catalogue, each eye's lens power (LensA1's +1.00 on the left, LensA2's +2.50 on the right,
    /// both single vision so no lens type), and the pupil-distance bucket a Sale is required to
    /// have. It also has to carry a Coating set: at least one entry is required on both branches,
    /// so a baseline with an empty one would not be valid.</summary>
    private static CreateSaleRequest ValidSale() => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Amina Okoro",
        FrameColourRefId = ActiveFrameColour,
        LensRangeType = LensRangeType.LensSet,
        PresetCatalogueId = CatalogueA,
        SphereLeft = 1.00m,
        SphereRight = 2.50m,
        PresetPupilDistanceBucket = 2,
        CoatingRefIds = [ActiveCoating],
    };

    /// <summary>A Test on a complete preset range. The bucket is left unset: optional on a Test.</summary>
    private static CreateTestRequest PresetTest() => new()
    {
        Id = Guid.NewGuid(),
        LensRangeType = LensRangeType.LensSet,
        PresetCatalogueId = CatalogueA,
        SphereLeft = 1.00m,
        SphereRight = 2.50m,
    };

    private static CreateLeadRequest PresetLead()
    {
        var request = ValidLead();
        request.LensRangeType = LensRangeType.LensSet;
        request.PresetCatalogueId = CatalogueA;
        request.SphereLeft = 1.00m;
        request.SphereRight = 2.50m;
        return request;
    }

    /// <summary>A Sale on CatalogueA's bifocal (+1.00, add +2.00) for both eyes.</summary>
    private static CreateSaleRequest BifocalSale()
    {
        var request = ValidSale();
        request.SphereLeft = 1.00m;
        request.AddLeft = 2.00m;
        request.SphereRight = 1.00m;
        request.AddRight = 2.00m;
        request.LensTypeRefId = ActiveLensType;
        return request;
    }

    /// <summary>A Test on a Custom prescription: both spheres, which is the minimum a Custom
    /// branch accepts. Pupil distance is optional on a Test, so it stays unset.</summary>
    private static CreateTestRequest CustomTest() => new()
    {
        Id = Guid.NewGuid(),
        LensRangeType = LensRangeType.Custom,
        SphereLeft = 1.00m,
        SphereRight = -0.50m,
    };

    private static CreateLeadRequest CustomLead()
    {
        var request = ValidLead();
        request.LensRangeType = LensRangeType.Custom;
        request.SphereLeft = 1.00m;
        request.SphereRight = -0.50m;
        return request;
    }

    /// <summary>A Sale on a Custom prescription. Unlike the Test and Lead it must carry a pupil
    /// distance — the order cannot be ground without one.</summary>
    private static CreateSaleRequest CustomSale()
    {
        var request = ValidSale();
        request.LensRangeType = LensRangeType.Custom;
        request.PresetCatalogueId = null;
        request.PresetPupilDistanceBucket = null;
        request.SphereLeft = 1.00m;
        request.SphereRight = -0.50m;
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
        Assert.Equal("Choose an occupation from the list.", failure.Message);
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
        Assert.Equal("Say what the other occupation is.", failure.Message);
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
        Assert.Equal("Tick \"Referred or treated\", or clear the referral details.", failure.Message);
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
        // Referred out with no location either: only the reason is asked for — the location is
        // optional.
        var request = ValidTest();
        request.ReferredOrTreated = true;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("ReferralReasonRefId", failure.Key);
        Assert.Equal("Choose a reason for the referral or treatment.", failure.Message);
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
        Assert.Equal("Choose a reason for the referral or treatment.", failure.Message);
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
        Assert.Equal("Say what the other referral reason is.", failure.Message);
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
        Assert.Equal("Clear the referral location, or untick \"Treated in facility\".", failure.Message);
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
    public void Referral_ReferredOutWithoutALocation_IsAccepted()
    {
        // The technician often doesn't know where the customer will go.
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.ReferralReasonRefId = ActiveReferralReason;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void Referral_ReferredOutWithWhitespaceOnlyLocation_IsAccepted()
    {
        var request = ValidTest();
        request.ReferredOrTreated = true;
        request.ReferralReasonRefId = ActiveReferralReason;
        request.ReferralLocationFreeText = "   ";

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void Referral_ReferredOutWithoutALocation_IsAcceptedOnALeadAndASale()
    {
        var lead = ValidLead();
        lead.ReferredOrTreated = true;
        lead.ReferralReasonRefId = ActiveReferralReason;

        var sale = ValidSale();
        sale.ReferredOrTreated = true;
        sale.ReferralReasonRefId = ActiveReferralReason;

        Assert.True(ConsultationRules.Check(lead, Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(sale, Snapshot()).IsValid);
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
        request.TreatedInFacility = true;
        request.ReferralLocationFreeText = "Kisumu District Hospital";

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(["ReferralReasonRefId", "ReferralLocationFreeText"], result.Failures.Select(f => f.Key));
    }

    [Fact]
    public void Referral_IsTheSameRuleOnASale()
    {
        var request = ValidSale();
        request.ReferredOrTreated = true;
        request.TreatedInFacility = true;
        request.ReferralLocationFreeText = "Kisumu District Hospital";

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(["ReferralReasonRefId", "ReferralLocationFreeText"], result.Failures.Select(f => f.Key));
    }

    [Fact]
    public void PriceAwareness_NotAnsweredOnALead_IsRejected()
    {
        var request = ValidLead();
        request.CustomerToldPrice = null;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CustomerToldPrice", failure.Key);
        Assert.Equal("Choose Yes or No for \"Has the customer been told the price?\".", failure.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PriceAwareness_EitherAnswer_IsAccepted(bool told)
    {
        // A note for whoever follows the Lead up, not a gate.
        var request = ValidLead();
        request.CustomerToldPrice = told;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
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
        Assert.Equal("Choose a reason not purchased.", failure.Message);
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
        Assert.Equal("Say what the other reason is.", failure.Message);
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
        Assert.Equal("Choose a frame colour.", failure.Message);
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
        Assert.Equal("Say what the other frame colour is.", failure.Message);
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
        Assert.Equal("Clear the hard case colour, or tick that a hard case was sold.", failure.Message);
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
        Assert.Equal("Choose a hard case colour.", failure.Message);
    }

    [Fact]
    public void HardCase_SoldWithARetiredColour_IsRejected()
    {
        var request = ValidSale();
        request.HardCaseSold = true;
        request.HardCaseColourRefId = RetiredHardCaseColour;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("HardCaseColourRefId", failure.Key);
        Assert.Equal("Choose a hard case colour.", failure.Message);
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
        Assert.Equal("Say what the other hard case colour is.", failure.Message);
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
            case "custom": request.SphereLeft = 1.00m; break;
            case "pupilDistance": request.PupilDistanceMm = 62m; break;
            case "bucket": request.PresetPupilDistanceBucket = 2; break;
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensRangeType", failure.Key);
        Assert.Equal("Lens set and custom lens fields must be empty when LensRangeType is not set.", failure.Message);
    }

    [Fact]
    public void LensRange_CustomChosenButAPresetFieldWasFilled_IsRejected()
    {
        var request = CustomTest();
        request.PresetCatalogueId = CatalogueA;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensRangeType", failure.Key);
        Assert.Equal("Lens set fields must be empty for a Custom LensRangeType.", failure.Message);
    }

    // --- Lens range: the preset branch ----------------------------------------------------

    [Fact]
    public void Preset_CompleteAndConsistent_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(PresetTest(), Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(PresetLead(), Snapshot()).IsValid);
        Assert.True(ConsultationRules.Check(ValidSale(), Snapshot()).IsValid);
    }

    [Fact]
    public void LensSet_WhicheverLensSetIsChosen_TheLensRangeIsTheSame()
    {
        // ADR-0005: the lens range says "a lens set"; which one is PresetCatalogueId's job. A Sale
        // on the second set is the same kind of lens range as one on the first — there is no
        // per-set kind for the two to disagree about.
        var request = ValidSale();
        request.LensRangeType = LensRangeType.LensSet;
        request.PresetCatalogueId = CatalogueB;
        request.SphereLeft = 3.00m;
        request.SphereRight = 3.00m;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void LensSet_Retired_IsRejectedAgainstTheLensSet()
    {
        // The server's snapshot keeps retired lens sets so historical records still resolve their
        // labels; "present" is therefore not enough, the set must also be active. Reported against
        // PresetCatalogueId so a Field App Failed record lands on the lens range control.
        var request = ValidSale();
        request.PresetCatalogueId = RetiredCatalogue;
        request.SphereLeft = 1.50m;
        request.SphereRight = 1.50m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("PresetCatalogueId", failure.Key);
        Assert.Equal("This lens set has been retired — choose another lens range.", failure.Message);
    }

    // --- Lens range: is the lens set available where the record is made? ---------------------

    [Theory]
    [InlineData("/1/2/3/4/", "/1/2/")]      // assigned at an ancestor — assignment cascades down
    [InlineData("/1/2/3/4/", "/1/2/3/4/")]  // assigned at the retail point itself
    [InlineData("/1/2/3/4/", "/1/9/", "/1/2/3/")] // any one assignment reaching it is enough
    public void LensSet_AssignedAtOrAboveTheRecordsLocation_IsAccepted(string location, params string[] assignedTo)
    {
        Assert.True(ConsultationRules.Check(ValidSale(), Snapshot(assignedTo).AtLocation(location)).IsValid);
    }

    [Theory]
    [InlineData("/1/40/5/", "/1/4/")]   // a sibling whose path merely starts with the same digits
    [InlineData("/1/2/3/4/", "/1/2/3/4/7/")] // assigned only *below* the record's location
    [InlineData("/1/2/3/4/")]           // assigned nowhere
    public void LensSet_NotAssignedAtOrAboveTheRecordsLocation_IsRejectedAgainstTheLensSet(string location, params string[] assignedTo)
    {
        var failure = AssertSingleFailure(ConsultationRules.Check(ValidSale(), Snapshot(assignedTo).AtLocation(location)));

        Assert.Equal("PresetCatalogueId", failure.Key);
        Assert.Equal("This lens set isn't available at this retail point — choose another lens range.", failure.Message);
    }

    [Fact]
    public void LensSet_ServerSnapshotWithNoLocation_FailsClosed()
    {
        // A server-side check that forgot AtLocation must not wave every lens set through.
        var failure = AssertSingleFailure(ConsultationRules.Check(ValidSale(), Snapshot(["/1/"])));

        Assert.Equal("PresetCatalogueId", failure.Key);
    }

    [Fact]
    public void Preset_MissingTheLensSet_ReportsOnceAndStops()
    {
        // Without a lens set there is nothing to match the lenses against, so the branch reports
        // the one thing the technician can act on and stops — continuing would complain that
        // powers they did choose are in no lens set.
        var request = PresetTest();
        request.PresetCatalogueId = null;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("PresetCatalogueId", failure.Key);
        Assert.Equal("PresetCatalogueId is required for a LensSet LensRangeType.", failure.Message);
    }

    [Fact]
    public void Preset_MissingTheLensSetStopsBeforeThePupilDistanceChecks()
    {
        // The same short-circuit, stated as the thing that actually matters: a Sale with no
        // catalogue reports the missing lens set alone, not that plus a required-bucket message.
        var request = ValidSale();
        request.PresetCatalogueId = null;
        request.PresetPupilDistanceBucket = null;

        Assert.Equal("PresetCatalogueId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Theory]
    [InlineData("left", "SphereLeft", "Choose a lens for the left eye.")]
    [InlineData("right", "SphereRight", "Choose a lens for the right eye.")]
    public void LensSet_AnEyeWithNoLensPower_IsReportedAgainstThatEyeAndStops(string eye, string key, string message)
    {
        // A lens-set record carries each eye's lens power, the same fields as a Custom
        // prescription (ADR-0007). An eye with none has no lens chosen — which is also how an
        // old-shape request arrives, naming its lenses by ids the request no longer has. Reported
        // against that eye's sphere, the key the lens dropdown renders against, and nothing else:
        // a missing bucket on top would be noise.
        var request = ValidSale();
        request.PresetPupilDistanceBucket = null;
        if (eye == "left")
        {
            request.SphereLeft = null;
        }
        else
        {
            request.SphereRight = null;
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal(key, failure.Key);
        Assert.Equal(message, failure.Message);
    }

    [Fact]
    public void LensSet_ALensPowerFromAnotherLensSet_IsRejectedAgainstThatEye()
    {
        // +3.00 is a real lens — LensB1 — but on the other lens set. Each eye is matched in the
        // chosen set only.
        var request = PresetTest();
        request.SphereLeft = 3.00m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("SphereLeft", failure.Key);
        Assert.Equal("No lens in this lens set has the left eye's lens power — choose a lens.", failure.Message);
    }

    [Theory]
    [InlineData("cylinder")]
    [InlineData("add")]
    [InlineData("sphere")]
    public void LensSet_EachEyeMustMatchALensByItsWholeLensPower(string differs)
    {
        // The power is sphere, cylinder, axis and add together — a +2.50 with a cylinder is not
        // the +2.50 in the set.
        var request = PresetTest();
        switch (differs)
        {
            case "cylinder": request.CylinderRight = -0.50m; request.AxisRight = 90m; break;
            case "add": request.AddRight = 1.00m; break;
            case "sphere": request.SphereRight = 2.75m; break;
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("SphereRight", failure.Key);
        Assert.Equal("No lens in this lens set has the right eye's lens power — choose a lens.", failure.Message);
    }

    [Fact]
    public void LensSet_BothEyesMatchingNothing_AreReportedSeparately()
    {
        var request = PresetTest();
        request.SphereLeft = 3.00m;
        request.SphereRight = 3.00m;

        Assert.Equal(["SphereLeft", "SphereRight"], ConsultationRules.Check(request, Snapshot()).Failures.Select(f => f.Key));
    }

    [Fact]
    public void LensSet_AnAddOf000WhereTheLensHasNone_IsTheSameLens()
    {
        // Matched the way LensPowerRules reads a power (LensSetLenses.Match): a 0.00 add is no add
        // and a 0.00 cylinder no cylinder.
        var request = PresetTest();
        request.AddLeft = 0.00m;
        request.CylinderRight = 0.00m;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void LensSet_ALensWithAnAddAndItsLensType_IsAccepted()
    {
        Assert.True(ConsultationRules.Check(BifocalSale(), Snapshot()).IsValid);
    }

    [Fact]
    public void LensSet_ALensWithAnAddButNoLensType_IsAMismatchedLensType()
    {
        // The lens type is part of what picks out the lens: +1.00 add +2.00 is only in the set as
        // a Bifocal, and a request that says "single vision" names no lens there.
        var request = BifocalSale();
        request.LensTypeRefId = null;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("LensTypeRefId must be the chosen lenses' own lens type.", failure.Message);
    }

    /// <summary>A lens set holding one lens of the "Other" lens type, with its own text — the only
    /// lens a lens-set record's LensTypeOtherText can be anything but empty for.</summary>
    private static ReferenceDataSnapshot SnapshotWithAnOtherLens(Guid lensSetId) => new(
        Snapshot().Items,
        [
            new PresetCatalogueSnapshot(lensSetId, "Other lens set", IsActive: true, [
                new LensOptionSnapshot(Guid.NewGuid(), "Trifocal +1.00", 1.00m, [ActiveCoating], Add: 2.00m, LensTypeRefId: OtherLensType, LensTypeOtherText: "Trifocal"),
            ], AssignedOrgPaths: null),
        ],
        []);

    [Fact]
    public void LensSet_TheLensTypeTextMustBeTheChosenLensesOwn()
    {
        var lensSetId = Guid.NewGuid();
        var request = BifocalSale();
        request.PresetCatalogueId = lensSetId;
        request.LensTypeRefId = OtherLensType;
        request.LensTypeOtherText = "Trifocal";

        Assert.True(ConsultationRules.Check(request, SnapshotWithAnOtherLens(lensSetId)).IsValid);

        request.LensTypeOtherText = "Varifocal";
        var failure = AssertSingleFailure(ConsultationRules.Check(request, SnapshotWithAnOtherLens(lensSetId)));

        Assert.Equal("LensTypeOtherText", failure.Key);
        Assert.Equal("LensTypeOtherText must be the chosen lenses' own lens type text (empty unless their lens type is \"Other\").", failure.Message);
    }

    [Fact]
    public void LensSet_ALensTypeTextOnALensWithoutOne_IsRefused()
    {
        // The lens set branch used to ignore the text entirely, so a record could carry free text
        // its Bifocal lens never had.
        var sale = BifocalSale();
        sale.LensTypeOtherText = "Trifocal";
        var test = PresetTest();
        test.LensTypeOtherText = "Trifocal";

        Assert.Equal("LensTypeOtherText", AssertSingleFailure(ConsultationRules.Check(sale, Snapshot())).Key);
        Assert.Equal("LensTypeOtherText", AssertSingleFailure(ConsultationRules.Check(test, Snapshot())).Key);
    }

    [Fact]
    public void LensSet_ALensTypeOnSingleVisionLenses_IsAMismatchedLensType()
    {
        var request = PresetTest();
        request.LensTypeRefId = ActiveLensType;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeRefId", failure.Key);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LensSet_AMixedPair_IsRefusedAgainstTheRightEye(bool lensTypeIsTheLeftLenss)
    {
        // Bifocal on the left, single vision on the right: each is a real lens in the set, but a
        // record has one lens type for the pair, so no one lens type can name both. Reported on
        // the right eye, which the Field App limits to the left eye's lens type.
        var request = ValidSale();
        request.SphereLeft = 1.00m;
        request.AddLeft = 2.00m;
        request.SphereRight = 2.50m;
        request.LensTypeRefId = lensTypeIsTheLeftLenss ? ActiveLensType : null;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("SphereRight", failure.Key);
        Assert.Equal("Both eyes' lenses must be the same lens type — choose a right-eye lens of the left eye's type.", failure.Message);
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
        Assert.Equal("PupilDistanceMm must be empty for a LensSet LensRangeType — use PresetPupilDistanceBucket instead.", failure.Message);
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
    public void Preset_OutOfRangeBucketSaysTheSameThingOnEveryRequestType()
    {
        // One rule, one sentence: whichever record it is, the thing to do is choose a pupil
        // distance from the list.
        var test = PresetTest();
        test.PresetPupilDistanceBucket = 9;
        var lead = PresetLead();
        lead.PresetPupilDistanceBucket = 9;
        var sale = ValidSale();
        sale.PresetPupilDistanceBucket = 9;

        Assert.Equal(
            "Choose a pupil distance between 0 and 4.",
            AssertSingleFailure(ConsultationRules.Check(test, Snapshot())).Message);
        Assert.Equal(
            "Choose a pupil distance between 0 and 4.",
            AssertSingleFailure(ConsultationRules.Check(lead, Snapshot())).Message);
        Assert.Equal(
            "Choose a pupil distance between 0 and 4.",
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
            "Choose a pupil distance between 0 and 2 — the limit for a children's frame.",
            AssertSingleFailure(ConsultationRules.Check(lead, Snapshot())).Message);
        Assert.Equal(
            "Choose a pupil distance between 0 and 2 — the limit for a children's frame.",
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
            request.SphereLeft = null;
        }
        else
        {
            request.SphereRight = null;
        }

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensRangeType", failure.Key);
        Assert.Equal("Choose a sphere for each eye.", failure.Message);
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
        request.SphereLeft = sphere;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            Assert.Equal("SphereLeft", Assert.Single(result.Failures).Key);
        }
    }

    [Fact]
    public void Custom_OffStepPowerNamesTheRangeAndTheStep()
    {
        var request = CustomTest();
        request.SphereLeft = 0.30m;

        Assert.Equal(
            "Sphere (left) must be between -10 and 10 in 0.25 increments.",
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
        // A lens type is set alongside any add above 0, because that is exactly what makes one
        // required — this case is about the power's range, not that requirement. An add of 0.00
        // is no add, so it takes no lens type.
        var request = CustomTest();
        request.AddLeft = addPower;
        request.LensTypeRefId = addPower > 0 ? ActiveLensType : null;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            var failure = Assert.Single(result.Failures);
            Assert.Equal("AddLeft", failure.Key);
            Assert.Equal("Add power (left) must be between 0 and 3 in 0.25 increments.", failure.Message);
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
        // With a cylinder, so the axis is asked for at all.
        var request = CustomTest();
        request.CylinderLeft = -1.00m;
        request.AxisLeft = axis;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(accepted, result.IsValid);
        if (!accepted)
        {
            var failure = Assert.Single(result.Failures);
            Assert.Equal("AxisLeft", failure.Key);
            Assert.Equal("Axis (left) must be a whole number of degrees between 0 and 180.", failure.Message);
        }
    }

    [Fact]
    public void Custom_BothEyesPowersAreCheckedIndependently()
    {
        var request = CustomTest();
        request.SphereLeft = 0.30m;
        request.CylinderRight = -20m;
        request.AxisRight = 200m;

        var result = ConsultationRules.Check(request, Snapshot());

        Assert.Equal(["SphereLeft", "CylinderRight", "AxisRight"], result.Failures.Select(f => f.Key));
    }

    [Theory]
    [InlineData(0.25)]   // the shop sells no positive cylinder
    [InlineData(4.00)]
    [InlineData(-6.25)]  // nor one below -6.00
    [InlineData(-10.00)]
    [InlineData(-0.30)]  // off the quarter step
    public void Custom_ACylinderTheShopDoesNotSell_IsRefusedAgainstThatEye(decimal cylinder)
    {
        var request = CustomTest();
        request.CylinderRight = cylinder;
        request.AxisRight = 90m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CylinderRight", failure.Key);
        Assert.Equal("Cylinder (right) must be between -6 and 0 in 0.25 increments.", failure.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-6)]
    [InlineData(-0.25)]
    public void Custom_ACylinderTheShopSells_IsAccepted(decimal cylinder)
    {
        var request = CustomSale();
        request.CylinderLeft = cylinder;
        request.AxisLeft = cylinder == 0 ? null : 45m;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void Custom_AxisIsRequiredWhenThatEyeHasACylinder()
    {
        var request = CustomLead();
        request.CylinderLeft = -1.25m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("AxisLeft", failure.Key);
        Assert.Equal("Axis (left) is required when Cylinder (left) isn't 0.00 — choose an axis from 0 to 180.", failure.Message);
    }

    [Theory]
    [InlineData(null)] // a blank cylinder means 0.00
    [InlineData(0)]
    public void Custom_AxisIsRefusedWhenThatEyeHasNoCylinder(object? cylinder)
    {
        var request = CustomSale();
        request.CylinderRight = cylinder is null ? null : Convert.ToDecimal(cylinder);
        request.AxisRight = 90m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("AxisRight", failure.Key);
        Assert.Equal("Axis (right) must be empty when Cylinder (right) is 0.00 — an axis only applies to a cylinder.", failure.Message);
    }

    [Fact]
    public void Custom_TheOtherEyesCylinderDoesNotAskForThisEyesAxis()
    {
        var request = CustomTest();
        request.CylinderLeft = -0.75m;
        request.AxisLeft = 10m;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    // --- Lens range: the lens type ---------------------------------------------------------

    [Fact]
    public void LensType_RequiredOnceAnEyeCarriesTwoDistinctPowers()
    {
        var request = CustomTest();
        request.AddLeft = 2.00m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("Choose a lens type — a lens with an add power needs one.", failure.Message);
    }

    [Fact]
    public void LensType_TheOtherEyesAddPowerTriggersItToo()
    {
        var request = CustomTest();
        request.AddRight = 2.00m;

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
        Assert.Equal("A lens type only applies to a lens with an add power — remove the lens type.", failure.Message);
    }

    [Fact]
    public void LensType_AnAddOfZeroIsNoAdd_SoItNeedsNoLensType()
    {
        // The shop treats an add of 0.00 as no add, and so does the Field App's lens type prompt.
        var request = CustomTest();
        request.AddLeft = 0.00m;
        request.AddRight = 0.00m;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void LensType_AnAddOfZeroIsNoAdd_SoALensTypeIsRefused()
    {
        var request = CustomSale();
        request.AddLeft = 0.00m;
        request.LensTypeRefId = ActiveLensType;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("A lens type only applies to a lens with an add power — remove the lens type.", failure.Message);
    }

    [Fact]
    public void LensType_OneForThePair_EvenWhenOnlyOneEyeHasAnAdd()
    {
        // A record carries one lens type for both eyes: an add on either eye makes the pair
        // bifocal/progressive, and the other eye's 0.00 add doesn't contradict it.
        var request = CustomLead();
        request.AddLeft = 0.00m;
        request.AddRight = 1.75m;
        request.LensTypeRefId = ActiveLensType;

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void LensType_RetiredItem_IsRejected()
    {
        var request = CustomTest();
        request.AddLeft = 2.00m;
        request.LensTypeRefId = RetiredLensType;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeRefId", failure.Key);
        Assert.Equal("Choose a lens type from the list.", failure.Message);
    }

    [Fact]
    public void LensType_ItemFromAnotherCategory_IsRejected()
    {
        var request = CustomTest();
        request.AddLeft = 2.00m;
        request.LensTypeRefId = ActiveOccupation;

        Assert.Equal("LensTypeRefId", AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Key);
    }

    [Fact]
    public void LensType_OtherWithoutFreeText_IsRejected()
    {
        var request = CustomTest();
        request.AddLeft = 2.00m;
        request.LensTypeRefId = OtherLensType;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("LensTypeOtherText", failure.Key);
        Assert.Equal("Say what the other lens type is.", failure.Message);
    }

    [Fact]
    public void LensType_OtherWithFreeText_IsAccepted()
    {
        var request = CustomTest();
        request.AddLeft = 2.00m;
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
            "Choose a pupil distance between 54 and 74 mm.",
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
            "Choose a pupil distance between 54 and 74 mm.",
            failure.Message);
    }

    [Fact]
    public void Custom_OutOfRangePupilDistanceOnASaleSaysTheSameAsAMissingOne()
    {
        // Missing or out of range, one sentence covers both: choose one from the list.
        var sale = CustomSale();
        sale.PupilDistanceMm = 80m;

        Assert.Equal(
            "Choose a pupil distance between 54 and 74 mm.",
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
        Assert.Equal("One of the chosen coatings isn't available any more — choose the coatings again.", failure.Message);
    }

    [Fact]
    public void CoatingSet_ContainingAnItemFromAnotherCategory_IsRejected()
    {
        // A Guid that resolves to a Frame colour is not an answer to "which Coating is this".
        var request = ValidSale();
        request.CoatingRefIds = [ActiveFrameColour];

        Assert.Equal(
            "One of the chosen coatings isn't available any more — choose the coatings again.",
            AssertSingleFailure(ConsultationRules.Check(request, Snapshot())).Message);
    }

    [Fact]
    public void CoatingSet_OnAPresetRange_RejectsACoatingNotConfiguredForTheChosenLens()
    {
        // UnavailableCoating is active and real — it is simply not one of this lens's own
        // coatings. That is the distinction between this rule and the active-item check above.
        var request = ValidSale();
        request.CoatingRefIds = [UnavailableCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("Every coating must be configured as available for the chosen lenses (see Lens Sets).", failure.Message);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CoatingSet_OnALensSet_OnlyCoatingsBothLensesComeIn_WhicheverEyeNarrowsThem(bool narrowLensOnTheLeft)
    {
        // One coating set for the pair (ADR-0007), so a coating has to be one both chosen lenses
        // come in. LensA1 (+1.00) comes in all three; LensA5 (+2.00) in ActiveCoating only.
        var request = ValidSale();
        (request.SphereLeft, request.SphereRight) = narrowLensOnTheLeft ? (2.00m, 1.00m) : (1.00m, 2.00m);
        request.CoatingRefIds = [SecondCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("Every coating must be configured as available for the chosen lenses (see Lens Sets).", failure.Message);

        request.CoatingRefIds = [ActiveCoating];
        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CoatingSet_ATriggerWithoutItsPairedCoating_IsRefused_WhicheverLensCarriesThePairing(bool pairedLensOnTheLeft)
    {
        // LensA6Paired pairs Blue Block → Photochromic; LensA1 carries no pairing. Both lenses'
        // pairings apply to the pair, so which eye the paired lens is on makes no difference.
        var request = ValidSale();
        (request.SphereLeft, request.SphereRight) = pairedLensOnTheLeft ? (0.50m, 1.00m) : (1.00m, 0.50m);
        request.CoatingRefIds = [SecondCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("Photochromic comes with Blue Block on these lenses — add Photochromic, or remove Blue Block.", failure.Message);
    }

    [Fact]
    public void CoatingSet_ATriggerWithItsPairedCoating_IsAccepted()
    {
        var request = ValidSale();
        (request.SphereLeft, request.SphereRight) = (0.50m, 1.00m);
        request.CoatingRefIds = [SecondCoating, ActiveCoating];

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingSet_APairingIsOneWay_SoThePairedCoatingAlone_IsAccepted()
    {
        // Blue Block needs Photochromic; Photochromic needs nothing (CONTEXT.md: directional).
        var request = ValidSale();
        (request.SphereLeft, request.SphereRight) = (0.50m, 1.00m);
        request.CoatingRefIds = [ActiveCoating];

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingSet_ATriggerWhosePairedCoatingOneLensDoesNotComeIn_IsNotOnOffer()
    {
        // Blue Block is in both LensA6Paired and LensA7NoPhotochromic, but on LensA6Paired it
        // comes with Photochromic, which LensA7NoPhotochromic doesn't come in. No coating set
        // holding Blue Block could be sold on this pair, so it isn't offered at all
        // (LensSetLenses.CoatingsFor) — the refusal is the availability one, not a pairing one
        // asking for a coating the technician can't add.
        var request = ValidSale();
        (request.SphereLeft, request.SphereRight) = (0.50m, 0.25m);
        request.CoatingRefIds = [SecondCoating];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("CoatingRefIds", failure.Key);
        Assert.Equal("Every coating must be configured as available for the chosen lenses (see Lens Sets).", failure.Message);

        request.CoatingRefIds = [ExcludingCoating];
        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingSet_TwoLensesWithNoCoatingBothCanBeMadeIn_IsReportedAgainstTheRightEye()
    {
        // LensA5 comes in ActiveCoating only, LensA7NoPhotochromic in everything but — nothing is
        // offered, so no choice of coating could satisfy the set. Like a mixed pair, it is the
        // right eye's lens to change (the Field App narrows the right eye to the left's).
        var request = ValidSale();
        (request.SphereLeft, request.SphereRight) = (2.00m, 0.25m);
        request.CoatingRefIds = [];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("SphereRight", failure.Key);
        Assert.Equal("No coating can be made on both of these lenses, so they can't be sold together on a lens set — choose another lens for the right eye.", failure.Message);
    }

    [Fact]
    public void CoatingSet_OnARightLensWithNoCoatingsConfigured_IsReportedAgainstTheRightEye()
    {
        var request = ValidSale();
        request.SphereRight = 3.50m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("SphereRight", failure.Key);
        Assert.Equal("This lens has no coatings configured yet, so it can't be sold on a lens set.", failure.Message);
    }

    [Fact]
    public void CoatingSet_OnACustomPrescription_NoPairingIsEnforced()
    {
        // Pairings belong to lens set lenses (ADR-0007); a custom prescription has none, so Blue
        // Block alone is fine there even though a lens set lens pairs it with Photochromic.
        var request = CustomSale();
        request.CoatingRefIds = [SecondCoating];

        Assert.True(ConsultationRules.Check(request, Snapshot()).IsValid);
    }

    [Theory]
    [InlineData("SphereLeft")]
    [InlineData("SphereRight")]
    public void CoatingSet_OnALensSetWhereAnEyeMatchesNoLens_ChecksTheCoatingsButNotAgainstTheLenses(string unmatchedEye)
    {
        // No pair of lenses means nothing to scope availability by; the lens-range rule has already
        // said so against the eye. The coatings are still checked for what doesn't depend on a
        // lens — an unavailable-but-real coating passes here, a retired one would not.
        var request = ValidSale();
        if (unmatchedEye == "SphereLeft")
        {
            request.SphereLeft = 3.00m;
        }
        else
        {
            request.SphereRight = 3.00m;
        }

        request.CoatingRefIds = [UnavailableCoating];

        Assert.Equal([unmatchedEye], ConsultationRules.Check(request, Snapshot()).Failures.Select(f => f.Key));

        request.CoatingRefIds = [RetiredCoating];
        Assert.Equal(
            new[] { unmatchedEye, "CoatingRefIds" }.Order(),
            ConsultationRules.Check(request, Snapshot()).Failures.Select(f => f.Key).Order());
    }

    [Fact]
    public void CoatingSet_OnALensWithNoCoatingsConfigured_IsReportedAgainstTheLensNotTheSet()
    {
        // The behaviour change ticket 11 made deliberately. IsCoatingAvailableForLensOption
        // returns false rather than throwing when a lens has no coatings of its own, so this
        // used to read "every coating must be configured as available for the chosen lens option"
        // against CoatingRefIds — advice no choice of coating could satisfy, because none is
        // available. It is the lens that has to change.
        var request = ValidSale();
        request.SphereLeft = 3.50m;

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("SphereLeft", failure.Key);
        Assert.Equal("This lens has no coatings configured yet, so it can't be sold on a lens set.", failure.Message);
    }

    [Fact]
    public void CoatingSet_OnALensWithNoCoatingsConfigured_SaysSoEvenWhenNoCoatingWasChosen()
    {
        // Asked ahead of "choose at least one coating": sending the technician to a picker with
        // nothing in it would be the one piece of advice they cannot act on.
        var request = ValidSale();
        request.SphereLeft = 3.50m;
        request.CoatingRefIds = [];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, Snapshot()));

        Assert.Equal("SphereLeft", failure.Key);
        Assert.Equal("This lens has no coatings configured yet, so it can't be sold on a lens set.", failure.Message);
    }

    /// <summary>A lens set whose lenses are read the way the server's snapshot reads them — each
    /// through LensSetLenses.WithActiveCoatingsOnly against Snapshot()'s active coatings: +1.00 comes
    /// only in the retired Anti-glare, +2.50 in Photochromic and the retired Anti-glare, with
    /// Photochromic → Anti-glare paired.</summary>
    private static ReferenceDataSnapshot SnapshotWithRetiredCoatingsOnLenses(Guid lensSetId)
    {
        var items = Snapshot().Items;
        var active = items.Where(i => i.Category == ReferenceDataCategory.Coating && i.IsActive).Select(i => i.Id).ToHashSet();
        LensOptionSnapshot Read(LensOptionSnapshot lens) => Rules.LensSets.LensSetLenses.WithActiveCoatingsOnly(lens, active);
        return new(
            items,
            [
                new PresetCatalogueSnapshot(lensSetId, "Retired coatings", IsActive: true, [
                    Read(new LensOptionSnapshot(Guid.NewGuid(), "+1.00", 1.00m, [RetiredCoating])),
                    Read(new LensOptionSnapshot(Guid.NewGuid(), "+2.50", 2.50m, [ActiveCoating, RetiredCoating],
                        Pairings: [new CoatingPairingRule(ActiveCoating, RetiredCoating)])),
                ], AssignedOrgPaths: null),
            ],
            []);
    }

    [Fact]
    public void CoatingSet_OnALensWhoseCoatingsAreAllRetired_IsReportedAgainstTheLens()
    {
        // Not "Choose at least one coating": nothing on this lens can be chosen.
        var lensSetId = Guid.NewGuid();
        var request = ValidSale();
        request.PresetCatalogueId = lensSetId;
        request.CoatingRefIds = [];

        var failure = AssertSingleFailure(ConsultationRules.Check(request, SnapshotWithRetiredCoatingsOnLenses(lensSetId)));

        Assert.Equal("SphereLeft", failure.Key);
        Assert.Equal("This lens has no coatings configured yet, so it can't be sold on a lens set.", failure.Message);
    }

    [Fact]
    public void CoatingSet_APairingWithARetiredCoating_NoLongerMakesItsTriggerUnsellable()
    {
        // Photochromic → Anti-glare with Anti-glare retired: read as it is sold now, the pairing is
        // gone rather than taking Photochromic off the lens.
        var lensSetId = Guid.NewGuid();
        var request = ValidSale();
        request.PresetCatalogueId = lensSetId;
        request.SphereLeft = 2.50m;
        request.CoatingRefIds = [ActiveCoating];

        Assert.True(ConsultationRules.Check(request, SnapshotWithRetiredCoatingsOnLenses(lensSetId)).IsValid);
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
        // No left lens means nothing to scope availability by, and telling a technician who has
        // not picked a lens yet to choose a coating would be noise on top of the real failure —
        // the same short-circuit the lens-range rule makes.
        var request = ValidSale();
        request.SphereLeft = null;
        request.CoatingRefIds = [];

        Assert.Equal(
            ["SphereLeft"],
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
        Assert.Equal("This coating preference isn't available any more — choose another, or no preference.", failure.Message);
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
        Assert.Equal("This coating preference isn't available for the chosen lenses — choose another, or no preference.", failure.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CoatingPreference_OnALensSet_MustBeOneBothLensesComeIn(bool narrowLensOnTheLeft)
    {
        // Limited to the same list a Sale's coating set is (ADR-0007), so the Sale a Lead later
        // converts into can honour it. LensA5 (+2.00) comes in ActiveCoating only.
        var (left, right) = narrowLensOnTheLeft ? (2.00m, 1.00m) : (1.00m, 2.00m);
        var test = PresetTest();
        (test.SphereLeft, test.SphereRight) = (left, right);
        test.CoatingPreferenceRefId = SecondCoating;
        var lead = PresetLead();
        (lead.SphereLeft, lead.SphereRight) = (left, right);
        lead.CoatingPreferenceRefId = SecondCoating;

        foreach (var result in new[] { ConsultationRules.Check(test, Snapshot()), ConsultationRules.Check(lead, Snapshot()) })
        {
            var failure = AssertSingleFailure(result);
            Assert.Equal("CoatingPreferenceRefId", failure.Key);
            Assert.Equal("This coating preference isn't available for the chosen lenses — choose another, or no preference.", failure.Message);
        }

        test.CoatingPreferenceRefId = ActiveCoating;
        Assert.True(ConsultationRules.Check(test, Snapshot()).IsValid);
    }

    [Fact]
    public void CoatingPreference_ATriggerWhosePairedCoatingOneLensDoesNotComeIn_IsNotOnOffer()
    {
        // The same offered list as a Sale's (LensSetLenses.CoatingsFor): Blue Block is in both
        // lenses but could never be sold on them, so it can't be preferred either.
        var test = PresetTest();
        (test.SphereLeft, test.SphereRight) = (0.50m, 0.25m);
        test.CoatingPreferenceRefId = SecondCoating;

        Assert.Equal("CoatingPreferenceRefId", AssertSingleFailure(ConsultationRules.Check(test, Snapshot())).Key);
    }

    [Fact]
    public void CoatingPreference_ATriggerWhosePairedCoatingIsOnOffer_IsAccepted()
    {
        // A preference is one coating, not a set, so no pairing is asked of it — the Sale's set
        // is where Photochromic has to join Blue Block.
        var test = PresetTest();
        (test.SphereLeft, test.SphereRight) = (0.50m, 1.00m);
        test.CoatingPreferenceRefId = SecondCoating;

        Assert.True(ConsultationRules.Check(test, Snapshot()).IsValid);
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
        test.SphereLeft = 3.50m;
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
            ["This coating preference isn't available for the chosen lenses — choose another, or no preference.",
             "This coating preference isn't available any more — choose another, or no preference."],
            testFailures.Select(f => f.Message));
        Assert.Equal(
            ["This coating preference isn't available any more — choose another, or no preference.",
             "This coating preference isn't available for the chosen lenses — choose another, or no preference."],
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
    // These pin the copy character-for-character: nothing but these assertions stands between a
    // client and a silently reworded message, and the Field App renders these strings verbatim
    // against the control that produced them. What a form control can cause is a plain instruction
    // naming the control; an empty Id and an out-of-enum value, which no form can cause, are still
    // FluentValidation's generated copy.

    [Fact]
    public void AnIdThatWasNeverFilledIn_IsRejected()
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
            new RuleFailure("OccupationOtherText", "Keep the other occupation to 200 characters or fewer."),
            result.Failures);
        Assert.Contains(
            new RuleFailure("ReferralLocationFreeText", "Keep the referral location to 500 characters or fewer."),
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
            new RuleFailure("AgeYears", "Enter an age between 0 and 120."),
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
            new RuleFailure("FullName", "Enter the customer's full name."),
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
            new RuleFailure("OrderFromDotGlasses", "Only a Custom prescription can be ordered from Dot Glasses — untick \"Order this lens from Dot Glasses\"."),
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
    public void EveryRequestCapsItsLensTypeOtherText()
    {
        // Once only a Test did; a lens set's lens now carries the text onto all three records, so
        // an over-long one is a keyed failure everywhere rather than a database error on a Lead or
        // Sale.
        var tooLong = new string('a', 201);
        var expected = new RuleFailure("LensTypeOtherText", "Keep the other lens type to 200 characters or fewer.");

        var test = CustomTest();
        test.AddLeft = 1.00m;
        test.LensTypeRefId = ActiveLensType;
        test.LensTypeOtherText = tooLong;

        var lead = CustomLead();
        lead.AddLeft = 1.00m;
        lead.LensTypeRefId = ActiveLensType;
        lead.LensTypeOtherText = tooLong;

        var sale = CustomSale();
        sale.AddLeft = 1.00m;
        sale.LensTypeRefId = ActiveLensType;
        sale.LensTypeOtherText = tooLong;

        Assert.Equal(expected, Assert.Single(ConsultationRules.Check(test, Snapshot()).Failures));
        Assert.Equal(expected, Assert.Single(ConsultationRules.Check(lead, Snapshot()).Failures));
        Assert.Equal(expected, Assert.Single(ConsultationRules.Check(sale, Snapshot()).Failures));
    }

    [Fact]
    public void OnlyASaleRangeChecksItsLensRangeType()
    {
        // Pre-existing drift, preserved rather than tidied when the scalars moved here (ticket 12):
        // a Sale range-checks LensRangeType and a Lead never has, though it carries the same enum.
        // Pinned so that harmonising it becomes a deliberate decision rather than an accident.
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
        request.TreatedInFacility = true;
        request.ReferralLocationFreeText = "Kisumu District Hospital";
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
        // catalogues — and the rules still have to answer rather than throw. Each eye's lens and
        // the Coating set are rejected for the same reason the occupation is: nothing in the
        // snapshot carries them.
        //
        // The Coating set reports "not an active Coating" rather than the lens-keyed
        // no-coatings-configured message, and that is the intended split: with an empty snapshot
        // the left eye matches no lens at all, which is a different failure that the lens-range
        // rule has already reported against SphereLeft.
        var request = ValidSale();
        request.OccupationRefId = ActiveOccupation;

        var result = ConsultationRules.Check(request, ReferenceDataSnapshot.Empty);

        Assert.Equal(
            ["OccupationRefId", "FrameColourRefId", "SphereLeft", "SphereRight", "CoatingRefIds"],
            result.Failures.Select(f => f.Key));
    }
}

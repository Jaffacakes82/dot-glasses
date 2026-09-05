using System.Collections;
using System.Reflection;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Rules.Sales;

namespace DotGlasses.Rules.Tests.Sales;

/// <summary>
/// <see cref="SaleAssembly"/> — the carry-over rule (<see cref="SaleAssembly.Seed"/>) and the
/// request assembly (<see cref="SaleAssembly.Build"/>) both write paths go through.
///
/// The first test below is the one that earns its keep over time: it walks
/// <see cref="CreateSaleRequest"/> by reflection and fails if a property is neither carried by the
/// builder nor listed as deliberately left out. Adding a field to the request and forgetting the
/// builder is exactly how the referral answers came to be missing from the Admin Portal's path,
/// and a hand-written per-field test cannot catch it because the field it should assert on does
/// not exist yet when the test is written.
/// </summary>
public class SaleAssemblyTests
{
    private static readonly Guid Occupation = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid ReferralReason = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid FrameColour = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    private static readonly Guid HardCaseColour = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    private static readonly Guid LensType = Guid.Parse("00000000-0000-0000-0000-00000000001a");
    private static readonly Guid Coating = Guid.Parse("00000000-0000-0000-0000-0000000000f1");
    private static readonly Guid CoatingPreference = Guid.Parse("00000000-0000-0000-0000-0000000000f2");

    /// <summary>
    /// Properties of <see cref="CreateSaleRequest"/> that <see cref="SaleAssembly.Build"/> does not
    /// take from <see cref="SaleAnswers"/>, each with the reason it cannot come from there. Both
    /// are identifiers the caller supplies as arguments, because neither is an answer a human gave
    /// on the form.
    /// </summary>
    private static readonly Dictionary<string, string> DeliberatelyNotCarried = new()
    {
        [nameof(CreateSaleRequest.Id)] =
            "the offline-sync outbox idempotency key — a Build argument, generated per save attempt "
            + "(or reused from the failed record being corrected), never an answer on the form.",
        [nameof(CreateSaleRequest.SourceLeadId)] =
            "the link to the Lead being converted — a Build argument, because on the Field App's "
            + "conversion-match path it is only decided at submit time, after the answers were given.",
    };

    // --- The coverage test -----------------------------------------------------------------

    [Fact]
    public void Every_CreateSaleRequest_property_is_carried_by_the_builder_or_listed_as_deliberately_not()
    {
        // Two probes because two rules are mutually exclusive: TreatedInFacility suppresses
        // ReferralLocationFreeText, so no single set of answers can carry both. A property counts
        // as carried when at least one probe puts the answer's own value on the request.
        var probes = new[] { Probe(treatedInFacility: false), Probe(treatedInFacility: true) };

        var missing = new List<string>();

        foreach (var requestProperty in typeof(CreateSaleRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (DeliberatelyNotCarried.ContainsKey(requestProperty.Name))
            {
                continue;
            }

            var answersProperty = typeof(SaleAnswers).GetProperty(requestProperty.Name, BindingFlags.Public | BindingFlags.Instance);
            if (answersProperty is null)
            {
                missing.Add($"{requestProperty.Name} — no property of that name on SaleAnswers, so nothing can supply it.");
                continue;
            }

            var carried = probes.Any(probe =>
                ValuesMatch(answersProperty.GetValue(probe.Answers), requestProperty.GetValue(probe.Request)));

            if (!carried)
            {
                missing.Add($"{requestProperty.Name} — SaleAnswers.{requestProperty.Name} exists but its value did not reach the request.");
            }
        }

        Assert.True(missing.Count == 0,
            "CreateSaleRequest has properties the shared builder does not handle:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing.Select(m => "  - " + m))
            + Environment.NewLine
            + Environment.NewLine
            + "Both write paths — the Field App's ConsultationForm.razor and the Admin Portal's "
            + "LeadConversionController — assemble Sale requests through SaleAssembly.Build, so a field it does "
            + "not handle silently reaches neither. To fix: add a matching property to SaleAnswers, assign it in "
            + "SaleAssembly.Build, and make sure both forms supply it. If the field genuinely cannot come from the "
            + "answers (an identifier the caller passes in, say), add it to DeliberatelyNotCarried above with the "
            + "reason — do not delete this assertion.");
    }

    /// <summary>Guards the guard: if the probe stops setting distinctive values, the coverage test
    /// above starts passing on defaults matching defaults and quietly stops testing anything.</summary>
    [Fact]
    public void Probe_answers_set_every_carried_property_to_a_non_default_value()
    {
        var answers = Probe(treatedInFacility: false).Answers;

        var defaulted = typeof(SaleAnswers).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !DeliberatelyNotCarried.ContainsKey(p.Name))
            .Where(p => p.Name != nameof(SaleAnswers.TreatedInFacility)) // false in this probe by construction; the other probe sets it
            .Where(p => IsDefault(p.GetValue(answers)))
            .Select(p => p.Name)
            .ToList();

        Assert.True(defaulted.Count == 0,
            "These SaleAnswers properties are left at their default in the probe, so the coverage test cannot tell "
            + "whether the builder carries them or merely leaves the request's own default in place: "
            + string.Join(", ", defaulted) + ". Give each one a distinctive value in Probe().");
    }

    // --- Carry-over -------------------------------------------------------------------------

    [Fact]
    public void Seed_carries_the_customer_and_the_consent_the_lead_recorded()
    {
        var seeded = SaleAssembly.Seed(LeadWithLens());

        Assert.Equal("Amina Okoro", seeded.FullName);
        Assert.Equal("+254700000000", seeded.PhoneNumber);
        Assert.Equal(42, seeded.AgeYears);
        Assert.Equal(Gender.Female, seeded.Gender);
        Assert.Equal(Occupation, seeded.OccupationRefId);
        Assert.Equal("Weaver", seeded.OccupationOtherText);
        Assert.True(seeded.ConsentGiven);
    }

    [Fact]
    public void Seed_carries_the_lens_block_when_the_lead_recorded_one()
    {
        var lead = LeadWithLens();

        Assert.True(SaleAssembly.CarriesLens(lead));

        var seeded = SaleAssembly.Seed(lead);

        Assert.Equal(LensRangeType.Custom, seeded.LensRangeType);
        Assert.Equal(-1.25m, seeded.CustomSphereLeft);
        Assert.Equal(-2.75m, seeded.CustomSphereRight);
        Assert.Equal(LensType, seeded.LensTypeRefId);
        Assert.Equal(62.5m, seeded.PupilDistanceMm);
        Assert.True(seeded.ChildrensFrame);
    }

    [Fact]
    public void Seed_leaves_the_lens_block_for_the_form_when_the_lead_recorded_none()
    {
        var lead = LeadWithLens();
        lead.LensRangeType = null;

        Assert.False(SaleAssembly.CarriesLens(lead));

        var seeded = SaleAssembly.Seed(lead);

        // Not merely the range: the whole block stays unset, so a Lead that half-recorded a
        // prescription cannot leak stray powers into a Sale whose range the form is about to ask for.
        Assert.Null(seeded.LensRangeType);
        Assert.Null(seeded.CustomSphereLeft);
        Assert.Null(seeded.LensTypeRefId);
        Assert.Null(seeded.PupilDistanceMm);
        Assert.False(seeded.ChildrensFrame);
    }

    /// <summary>CONTEXT.md — a Lead carries a single <b>Coating preference</b>, and converting to a
    /// Sale seeds its <b>Coating set</b> from it. The two are different concepts and this is the one
    /// place they meet.</summary>
    [Fact]
    public void Seed_seeds_the_coating_set_from_the_leads_single_coating_preference()
    {
        var seeded = SaleAssembly.Seed(LeadWithLens());

        Assert.Equal([CoatingPreference], seeded.CoatingRefIds);
    }

    [Fact]
    public void Seed_seeds_an_empty_coating_set_when_the_lead_expressed_no_preference()
    {
        var lead = LeadWithLens();
        lead.CoatingPreferenceRefId = null;

        Assert.Empty(SaleAssembly.Seed(lead).CoatingRefIds);
    }

    /// <summary>Test/Lead/Sale are separate create-once events and each asks "referred or treated"
    /// fresh — carrying the Lead's answer forward would record a referral that did not happen at
    /// this visit.</summary>
    [Fact]
    public void Seed_does_not_carry_the_leads_referral_answers()
    {
        var lead = LeadWithLens();
        lead.ReferredOrTreated = true;
        lead.ReferralReasonRefId = ReferralReason;
        lead.ReferralLocationFreeText = "Kisumu clinic";
        lead.TreatedInFacility = true;

        var seeded = SaleAssembly.Seed(lead);

        Assert.False(seeded.ReferredOrTreated);
        Assert.Null(seeded.ReferralReasonRefId);
        Assert.Null(seeded.ReferralLocationFreeText);
        Assert.False(seeded.TreatedInFacility);
    }

    /// <summary>Frame colour and hard case are decisions made at the point of sale — no Lead could
    /// have recorded them.</summary>
    [Fact]
    public void Seed_does_not_invent_point_of_sale_answers()
    {
        var seeded = SaleAssembly.Seed(LeadWithLens());

        Assert.Null(seeded.FrameColourRefId);
        Assert.False(seeded.HardCaseSold);
        Assert.Null(seeded.HardCaseColourRefId);
        Assert.False(seeded.OrderFromDotGlasses);
    }

    // --- Assembly ---------------------------------------------------------------------------

    [Fact]
    public void Build_takes_the_id_and_the_source_lead_link_as_arguments()
    {
        var id = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        var request = SaleAssembly.Build(id, leadId, new SaleAnswers());

        Assert.Equal(id, request.Id);
        Assert.Equal(leadId, request.SourceLeadId);
    }

    [Fact]
    public void Build_leaves_the_source_lead_link_unset_for_a_sale_made_from_scratch()
    {
        Assert.Null(SaleAssembly.Build(Guid.NewGuid(), null, new SaleAnswers()).SourceLeadId);
    }

    [Fact]
    public void Build_blanks_the_referral_detail_when_the_answer_is_no()
    {
        var request = SaleAssembly.Build(Guid.NewGuid(), null, new SaleAnswers
        {
            ReferredOrTreated = false,
            ReferralReasonRefId = ReferralReason,
            ReferralOtherText = "left over",
            ReferralLocationFreeText = "left over",
            TreatedInFacility = true,
        });

        Assert.False(request.ReferredOrTreated);
        Assert.Null(request.ReferralReasonRefId);
        Assert.Null(request.ReferralOtherText);
        Assert.Null(request.ReferralLocationFreeText);
        Assert.False(request.TreatedInFacility);
    }

    [Fact]
    public void Build_blanks_the_referral_location_when_treatment_happened_in_the_facility()
    {
        var request = SaleAssembly.Build(Guid.NewGuid(), null, new SaleAnswers
        {
            ReferredOrTreated = true,
            ReferralReasonRefId = ReferralReason,
            TreatedInFacility = true,
            ReferralLocationFreeText = "left over",
        });

        Assert.True(request.TreatedInFacility);
        Assert.Null(request.ReferralLocationFreeText);
        Assert.Equal(ReferralReason, request.ReferralReasonRefId);
    }

    [Fact]
    public void Build_blanks_the_hard_case_colour_when_no_case_was_sold()
    {
        var request = SaleAssembly.Build(Guid.NewGuid(), null, new SaleAnswers
        {
            HardCaseSold = false,
            HardCaseColourRefId = HardCaseColour,
            HardCaseOtherColourText = "left over",
        });

        Assert.False(request.HardCaseSold);
        Assert.Null(request.HardCaseColourRefId);
        Assert.Null(request.HardCaseOtherColourText);
    }

    /// <summary>No colour chosen must not become an invented one. The request's field is
    /// non-nullable so it carries the default, which is not a real FrameColour id — the rules module
    /// rejects it keyed on the field the person has to go back and answer.</summary>
    [Fact]
    public void Build_does_not_invent_a_frame_colour_that_was_never_chosen()
    {
        var request = SaleAssembly.Build(Guid.NewGuid(), null, new SaleAnswers { FrameColourRefId = null });

        Assert.Equal(Guid.Empty, request.FrameColourRefId);
        Assert.DoesNotContain(request.FrameColourRefId, new[] { FrameColour, Coating, HardCaseColour });
    }

    [Fact]
    public void Build_normalises_a_blank_phone_number_to_absent()
    {
        Assert.Null(SaleAssembly.Build(Guid.NewGuid(), null, new SaleAnswers { PhoneNumber = "   " }).PhoneNumber);
    }

    /// <summary>Deliberately not gated here — the two forms need opposite things from the "Custom
    /// range only" condition, so each applies it where it gathers answers. See
    /// SaleAnswers.OrderFromDotGlasses.</summary>
    [Fact]
    public void Build_passes_order_from_dot_glasses_through_as_supplied()
    {
        var request = SaleAssembly.Build(Guid.NewGuid(), null, new SaleAnswers
        {
            LensRangeType = LensRangeType.SixLensSet,
            OrderFromDotGlasses = true,
        });

        Assert.True(request.OrderFromDotGlasses);
    }

    // --- Both paths agree -------------------------------------------------------------------

    /// <summary>
    /// The point of the whole exercise: the Admin Portal seeds at build time and the Field App seeds
    /// at load time into its controls, and both must land on the same request. Modelled here as the
    /// two orders those paths apply the same inputs in.
    /// </summary>
    [Fact]
    public void Both_write_paths_assemble_the_same_request_from_the_same_answers()
    {
        var lead = LeadWithLens();
        var id = Guid.NewGuid();

        // Admin Portal: hold the Lead and the form as two objects, seed at build time.
        var adminAnswers = SaleAssembly.Seed(lead) with
        {
            FrameColourRefId = FrameColour,
            CoatingRefIds = [Coating],
            HardCaseSold = true,
            HardCaseColourRefId = HardCaseColour,
        };

        // Field App: the Lead was applied into the controls at load time, so by build time the
        // answers are simply what the controls hold.
        var seeded = SaleAssembly.Seed(lead);
        var fieldAnswers = new SaleAnswers
        {
            FullName = seeded.FullName,
            PhoneNumber = seeded.PhoneNumber,
            AgeYears = seeded.AgeYears,
            Gender = seeded.Gender,
            OccupationRefId = seeded.OccupationRefId,
            OccupationOtherText = seeded.OccupationOtherText,
            ConsentGiven = seeded.ConsentGiven,
            FrameColourRefId = FrameColour,
            CoatingRefIds = [Coating],
            HardCaseSold = true,
            HardCaseColourRefId = HardCaseColour,
        }.WithLens(
            seeded.LensRangeType, seeded.PresetCatalogueId, seeded.LensOptionLeftId, seeded.LensOptionRightId,
            seeded.CustomSphereLeft, seeded.CustomCylinderLeft, seeded.CustomAxisLeft, seeded.CustomAddPowerLeft,
            seeded.CustomSphereRight, seeded.CustomCylinderRight, seeded.CustomAxisRight, seeded.CustomAddPowerRight,
            seeded.LensTypeRefId, seeded.LensTypeOtherText,
            seeded.PupilDistanceMm, seeded.PresetPupilDistanceBucket, seeded.ChildrensFrame);

        var fromAdmin = SaleAssembly.Build(id, lead.Id, adminAnswers);
        var fromField = SaleAssembly.Build(id, lead.Id, fieldAnswers);

        foreach (var property in typeof(CreateSaleRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.True(
                ValuesMatch(property.GetValue(fromAdmin), property.GetValue(fromField)),
                $"{property.Name} differs between the two write paths: "
                + $"admin={property.GetValue(fromAdmin)}, field={property.GetValue(fromField)}");
        }
    }

    // --- Helpers ----------------------------------------------------------------------------

    private static (SaleAnswers Answers, CreateSaleRequest Request) Probe(bool treatedInFacility)
    {
        var answers = new SaleAnswers
        {
            FullName = "Amina Okoro",
            PhoneNumber = "+254700000000",
            AgeYears = 42,
            Gender = Gender.Male,
            OccupationRefId = Occupation,
            OccupationOtherText = "Weaver",
            ConsentGiven = true,
            ReferredOrTreated = true,
            ReferralReasonRefId = ReferralReason,
            ReferralOtherText = "Referral other",
            ReferralLocationFreeText = treatedInFacility ? null : "Kisumu clinic",
            TreatedInFacility = treatedInFacility,
            OrderFromDotGlasses = true,
            FrameColourRefId = FrameColour,
            FrameColourOtherText = "Frame colour other",
            FrameCoverage = FrameCoverage.EyeFrameRimsOnly,
            CoatingRefIds = [Coating],
            HardCaseSold = true,
            HardCaseColourRefId = HardCaseColour,
            HardCaseOtherColourText = "Hard case other",
        }.WithLens(
            LensRangeType.Custom, Guid.Parse("00000000-0000-0000-0000-000000000c01"),
            Guid.Parse("00000000-0000-0000-0000-000000000c02"), Guid.Parse("00000000-0000-0000-0000-000000000c03"),
            -1.25m, -0.75m, 90m, 2.00m,
            -2.75m, -1.50m, 180m, 2.50m,
            LensType, "Lens type other",
            62.5m, 3, true);

        return (answers, SaleAssembly.Build(Guid.NewGuid(), Guid.NewGuid(), answers));
    }

    private static LeadDto LeadWithLens() => new()
    {
        Id = Guid.NewGuid(),
        HierarchyPath = "/1/2/3/",
        TechnicianUserId = Guid.NewGuid(),
        CustomerFullName = "Amina Okoro",
        CustomerPhoneNumber = "+254700000000",
        AgeYears = 42,
        Gender = Gender.Female,
        OccupationRefId = Occupation,
        OccupationOtherText = "Weaver",
        ConsentGiven = true,
        CoatingPreferenceRefId = CoatingPreference,
        LensRangeType = LensRangeType.Custom,
        CustomSphereLeft = -1.25m,
        CustomCylinderLeft = -0.75m,
        CustomAxisLeft = 90m,
        CustomAddPowerLeft = 2.00m,
        CustomSphereRight = -2.75m,
        CustomCylinderRight = -1.50m,
        CustomAxisRight = 180m,
        CustomAddPowerRight = 2.50m,
        LensTypeRefId = LensType,
        LensTypeOtherText = "Lens type other",
        PupilDistanceMm = 62.5m,
        ChildrensFrame = true,
    };

    /// <summary>Sequence equality for the Coating set, plain equality otherwise — the builder hands
    /// the answers' own list through, but comparing by reference would make the coverage test pass
    /// for a reason unrelated to the field being carried.</summary>
    private static bool ValuesMatch(object? answerValue, object? requestValue)
    {
        if (answerValue is IEnumerable answerItems and not string && requestValue is IEnumerable requestItems and not string)
        {
            return answerItems.Cast<object>().SequenceEqual(requestItems.Cast<object>());
        }

        return Equals(answerValue, requestValue);
    }

    private static bool IsDefault(object? value) => value switch
    {
        null => true,
        string text => text.Length == 0,
        IEnumerable items and not string => !items.Cast<object>().Any(),
        _ => value.Equals(Activator.CreateInstance(value.GetType())),
    };
}

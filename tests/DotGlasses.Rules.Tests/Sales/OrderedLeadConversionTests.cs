using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Rules.Sales;

namespace DotGlasses.Rules.Tests.Sales;

/// <summary>
/// The lock on converting a Lead whose lens is already ordered (ADR-0008): the Sale keeps the
/// lens and the Coating set that were ordered and asks for no second order. Each failure is keyed
/// on the Sale request's own property, because the Sale endpoint and the Admin Portal's conversion
/// screen both put it against a control.
/// </summary>
public class OrderedLeadConversionTests
{
    private static readonly Guid BlueBlock = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Photochromic = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Bifocal = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static LeadDto AnOrderedLead() => new()
    {
        Id = Guid.NewGuid(),
        CustomerFullName = "Amina Okoro",
        LensRangeType = LensRangeType.Custom,
        SphereLeft = -1.25m,
        CylinderLeft = -0.75m,
        AxisLeft = 90m,
        AddLeft = 2.00m,
        SphereRight = -2.75m,
        AddRight = 2.00m,
        LensTypeRefId = Bifocal,
        PupilDistanceMm = 62m,
        OrderFromDotGlasses = true,
        CoatingRefIds = [BlueBlock, Photochromic],
        CustomOrderStatus = CustomOrderStatus.Submitted,
    };

    /// <summary>The Sale a locked form sends: the Lead's lens and coatings, unchanged.</summary>
    private static CreateSaleRequest ASaleKeeping(LeadDto lead) => new()
    {
        Id = Guid.NewGuid(),
        SourceLeadId = lead.Id,
        FullName = lead.CustomerFullName,
        LensRangeType = lead.LensRangeType!.Value,
        PresetCatalogueId = lead.PresetCatalogueId,
        SphereLeft = lead.SphereLeft,
        CylinderLeft = lead.CylinderLeft,
        AxisLeft = lead.AxisLeft,
        AddLeft = lead.AddLeft,
        SphereRight = lead.SphereRight,
        CylinderRight = lead.CylinderRight,
        AxisRight = lead.AxisRight,
        AddRight = lead.AddRight,
        LensTypeRefId = lead.LensTypeRefId,
        LensTypeOtherText = lead.LensTypeOtherText,
        PupilDistanceMm = lead.PupilDistanceMm,
        CoatingRefIds = [.. lead.CoatingRefIds],
    };

    [Fact]
    public void A_sale_keeping_the_ordered_lens_and_coatings_is_accepted()
    {
        var lead = AnOrderedLead();

        Assert.True(OrderedLeadConversion.Check(ASaleKeeping(lead), lead).IsValid);
    }

    [Fact]
    public void A_lead_that_placed_no_order_locks_nothing()
    {
        var lead = AnOrderedLead();
        lead.OrderFromDotGlasses = false;
        lead.CoatingRefIds = [];
        lead.CustomOrderStatus = null;

        var sale = ASaleKeeping(lead);
        sale.SphereLeft = -4.00m;
        sale.CoatingRefIds = [BlueBlock];
        sale.OrderFromDotGlasses = true;

        Assert.True(OrderedLeadConversion.Check(sale, lead).IsValid);
    }

    public static TheoryData<string, Action<CreateSaleRequest>> LensChanges => new()
    {
        { "LensRangeType", s => s.LensRangeType = LensRangeType.LensSet },
        { "PresetCatalogueId", s => s.PresetCatalogueId = Guid.NewGuid() },
        { "SphereLeft", s => s.SphereLeft = -1.50m },
        { "CylinderLeft", s => s.CylinderLeft = -1.00m },
        { "AxisLeft", s => s.AxisLeft = 180m },
        { "AddLeft", s => s.AddLeft = 2.50m },
        { "SphereRight", s => s.SphereRight = -3.00m },
        { "AddRight", s => s.AddRight = 1.00m },
        { "LensTypeRefId", s => s.LensTypeRefId = Guid.NewGuid() },
        { "LensTypeOtherText", s => s.LensTypeOtherText = "Office" },
        { "PupilDistanceMm", s => s.PupilDistanceMm = 64m },
    };

    [Theory]
    [MemberData(nameof(LensChanges))]
    public void A_changed_lens_is_refused_against_the_field_that_changed(string field, Action<CreateSaleRequest> change)
    {
        var lead = AnOrderedLead();
        var sale = ASaleKeeping(lead);
        change(sale);

        Assert.Equal(
            new RuleFailure(field, OrderedLeadConversion.LensLockedMessage),
            Assert.Single(OrderedLeadConversion.Check(sale, lead).Failures));
    }

    [Fact]
    public void A_cylinder_gained_on_the_eye_that_had_none_is_refused_with_its_axis()
    {
        var lead = AnOrderedLead();
        var sale = ASaleKeeping(lead);
        sale.CylinderRight = -0.50m;
        sale.AxisRight = 45m;

        Assert.Equal(["CylinderRight", "AxisRight"], OrderedLeadConversion.Check(sale, lead).Failures.Select(f => f.Key));
    }

    /// <summary>Compared as a record stores a power (LensPowerRules.Normalise): a zero cylinder is
    /// no cylinder, so sending one back for an eye that has none is the same lens.</summary>
    [Fact]
    public void A_power_spelled_differently_but_stored_the_same_is_not_a_change()
    {
        var lead = AnOrderedLead();
        var sale = ASaleKeeping(lead);
        sale.CylinderRight = 0.00m;

        Assert.True(OrderedLeadConversion.Check(sale, lead).IsValid);
    }

    [Theory]
    [InlineData(null, " ")]
    [InlineData("Office", " Office ")]
    public void Lens_type_text_differing_only_by_blank_space_is_not_a_change(string? ordered, string? sent)
    {
        var lead = AnOrderedLead();
        lead.LensTypeOtherText = ordered;
        var sale = ASaleKeeping(lead);
        sale.LensTypeOtherText = sent;

        Assert.True(OrderedLeadConversion.Check(sale, lead).IsValid);
    }

    [Fact]
    public void A_changed_coating_set_is_refused_against_the_coatings()
    {
        var lead = AnOrderedLead();

        foreach (var coatings in new List<Guid>[] { [BlueBlock], [BlueBlock, Photochromic, Guid.NewGuid()], [] })
        {
            var sale = ASaleKeeping(lead);
            sale.CoatingRefIds = coatings;

            Assert.Equal(
                new RuleFailure("CoatingRefIds", OrderedLeadConversion.CoatingsLockedMessage),
                Assert.Single(OrderedLeadConversion.Check(sale, lead).Failures));
        }
    }

    [Fact]
    public void The_same_coatings_in_another_order_are_the_same_set()
    {
        var lead = AnOrderedLead();
        var sale = ASaleKeeping(lead);
        sale.CoatingRefIds = [Photochromic, BlueBlock];

        Assert.True(OrderedLeadConversion.Check(sale, lead).IsValid);
    }

    [Fact]
    public void Asking_for_a_second_order_is_refused_against_the_tick()
    {
        var lead = AnOrderedLead();
        var sale = ASaleKeeping(lead);
        sale.OrderFromDotGlasses = true;

        Assert.Equal(
            new RuleFailure("OrderFromDotGlasses", OrderedLeadConversion.AlreadyOrderedMessage),
            Assert.Single(OrderedLeadConversion.Check(sale, lead).Failures));
    }

    // --- The lock over the ordinary Sale rules -------------------------------------------------

    private static readonly RuleFailure RetiredCoating = new("CoatingRefIds", "One of the coatings is no longer offered. Choose the coatings again.");
    private static readonly RuleFailure RetiredLensType = new("LensTypeRefId", "Choose a lens type.");
    private static readonly RuleFailure NoFrameColour = new("FrameColourRefId", "Choose a frame colour.");

    /// <summary>The lens and coatings were checked when the order was placed and are being made.
    /// A coating or lens type retired since must not make the order impossible to pay for — the
    /// technician can't change a locked set, and there is no cancel.</summary>
    [Fact]
    public void For_an_ordered_lead_the_lock_replaces_the_ordinary_rules_on_the_locked_fields()
    {
        var lead = AnOrderedLead();
        var saleRules = RuleResult.From([RetiredCoating, RetiredLensType, NoFrameColour]);

        var result = OrderedLeadConversion.Over(saleRules, ASaleKeeping(lead), lead);

        // What the Sale itself still has to answer is untouched.
        Assert.Equal([NoFrameColour], result.Failures);
    }

    [Fact]
    public void A_changed_lens_is_still_refused_once_with_the_locks_own_message()
    {
        var lead = AnOrderedLead();
        var sale = ASaleKeeping(lead);
        sale.CoatingRefIds = [BlueBlock];

        var result = OrderedLeadConversion.Over(RuleResult.From([RetiredCoating]), sale, lead);

        Assert.Equal([new RuleFailure("CoatingRefIds", OrderedLeadConversion.CoatingsLockedMessage)], result.Failures);
    }

    [Fact]
    public void For_a_lead_that_placed_no_order_the_ordinary_rules_stand_as_they_are()
    {
        var lead = AnOrderedLead();
        lead.OrderFromDotGlasses = false;
        lead.CoatingRefIds = [];
        var saleRules = RuleResult.From([RetiredCoating, NoFrameColour]);

        Assert.Same(saleRules, OrderedLeadConversion.Over(saleRules, ASaleKeeping(lead), lead));
    }

    [Fact]
    public void Every_field_the_lock_compares_is_a_locked_key()
    {
        var lead = AnOrderedLead();
        foreach (var (field, change) in LensChanges.Select(row => ((string)row[0], (Action<CreateSaleRequest>)row[1])))
        {
            Assert.Contains(field, OrderedLeadConversion.LockedKeys);
        }

        Assert.Contains("CoatingRefIds", OrderedLeadConversion.LockedKeys);

        // The tick is refused by the lock but is not a locked field: an ordinary rule about it
        // (there is none today) would still be reported.
        Assert.DoesNotContain("OrderFromDotGlasses", OrderedLeadConversion.LockedKeys);
        Assert.NotNull(lead);
    }

    /// <summary>These reach a technician on Failed records, so they follow the same voice as every
    /// other rule message: a plain sentence, no property names.</summary>
    [Fact]
    public void The_messages_are_plain_sentences()
    {
        foreach (var message in new[] { OrderedLeadConversion.LensLockedMessage, OrderedLeadConversion.CoatingsLockedMessage, OrderedLeadConversion.AlreadyOrderedMessage })
        {
            Assert.EndsWith(".", message);
            Assert.DoesNotContain("CoatingRefIds", message);
            Assert.DoesNotContain("OrderFromDotGlasses", message);
            Assert.DoesNotContain(" must ", message);
        }
    }
}

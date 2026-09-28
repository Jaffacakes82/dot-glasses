using DotGlasses.Contracts.Common;
using DotGlasses.Rules.LensSets;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests.LensSets;

/// <summary>
/// The three helpers every screen that lists, matches or offers coatings for a lens set's lenses
/// shares (ADR-0007): the fixed display order, matching a lens power and lens type to a lens, and
/// the coatings and pairings a chosen pair offers.
/// </summary>
public class LensSetLensesTests
{
    private static readonly Guid Bifocal = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid Progressive = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid Other = Guid.Parse("dddddddd-0000-0000-0000-000000000003");

    private static readonly Guid BlueBlock = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Photochromic = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Clear = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid AntiGlare = Guid.Parse("cccccccc-0000-0000-0000-000000000004");

    /// <summary>The LensType list as both fillings carry it: in its admin-set order, which is
    /// seeded Bifocal, Progressive, Other.</summary>
    private static readonly IReadOnlyList<ReferenceItemSnapshot> LensTypes =
    [
        new(Bifocal, ReferenceDataCategory.LensType, "Bifocal", IsActive: true, IsOtherOption: false),
        new(Progressive, ReferenceDataCategory.LensType, "Progressive", IsActive: true, IsOtherOption: false),
        new(Other, ReferenceDataCategory.LensType, "Other", IsActive: true, IsOtherOption: true),
    ];

    private static LensOptionSnapshot Lens(
        string label, decimal sphere, decimal? add = null, Guid? lensType = null,
        decimal? cylinder = null, decimal? axis = null, IReadOnlyList<Guid>? coatings = null,
        IReadOnlyList<CoatingPairingRule>? pairings = null) =>
        new(Guid.NewGuid(), label, sphere, coatings ?? [Clear], cylinder, axis, add, lensType, Pairings: pairings);

    [Fact]
    public void InDisplayOrder_SingleVisionBySphere_ThenBifocalProgressiveOther_EachByAddThenSphere()
    {
        var mixed = new[]
        {
            Lens("Other +1.00", 1.00m, add: 1.00m, lensType: Other),
            Lens("Progressive +2.00", 0.50m, add: 2.00m, lensType: Progressive),
            Lens("+2.50", 2.50m),
            Lens("Bifocal +2.50", 0.00m, add: 2.50m, lensType: Bifocal),
            Lens("-1.00", -1.00m),
            Lens("Bifocal +1.50 (+1.00)", 1.00m, add: 1.50m, lensType: Bifocal),
            Lens("Progressive +1.00", 0.00m, add: 1.00m, lensType: Progressive),
            Lens("Bifocal +1.50", 0.00m, add: 1.50m, lensType: Bifocal),
            Lens("+1.00", 1.00m),
        };

        var ordered = LensSetLenses.InDisplayOrder(mixed, LensTypes);

        Assert.Equal(
            [
                "-1.00", "+1.00", "+2.50",
                "Bifocal +1.50", "Bifocal +1.50 (+1.00)", "Bifocal +2.50",
                "Progressive +1.00", "Progressive +2.00",
                "Other +1.00",
            ],
            ordered.Select(l => l.Label));
    }

    [Fact]
    public void InDisplayOrder_LensTypesFollowTheirListOrder_WithOtherLast_AndAnUnlistedTypeAfterIt()
    {
        // An admin has moved Progressive above Bifocal and added "Trifocal" after Other; a lens
        // points at a lens type this copy doesn't hold (retired, on the device).
        var trifocal = Guid.Parse("dddddddd-0000-0000-0000-000000000004");
        var retired = Guid.Parse("dddddddd-0000-0000-0000-00000000ffff");
        IReadOnlyList<ReferenceItemSnapshot> reordered =
        [
            new(Progressive, ReferenceDataCategory.LensType, "Progressive", IsActive: true, IsOtherOption: false),
            new(Other, ReferenceDataCategory.LensType, "Other", IsActive: true, IsOtherOption: true),
            new(Bifocal, ReferenceDataCategory.LensType, "Bifocal", IsActive: true, IsOtherOption: false),
            new(trifocal, ReferenceDataCategory.LensType, "Trifocal", IsActive: true, IsOtherOption: false),
        ];
        var lenses = new[]
        {
            Lens("Retired", 0.00m, add: 1.00m, lensType: retired),
            Lens("Other", 0.00m, add: 1.00m, lensType: Other),
            Lens("Trifocal", 0.00m, add: 1.00m, lensType: trifocal),
            Lens("Bifocal", 0.00m, add: 1.00m, lensType: Bifocal),
            Lens("Progressive", 0.00m, add: 1.00m, lensType: Progressive),
        };

        var ordered = LensSetLenses.InDisplayOrder(lenses, reordered);

        Assert.Equal(["Progressive", "Bifocal", "Trifocal", "Other", "Retired"], ordered.Select(l => l.Label));
    }

    [Fact]
    public void InDisplayOrder_DoesNotDependOnTheOrderTheLensesArrivedIn()
    {
        var lenses = new[]
        {
            Lens("+1.50", 1.50m),
            Lens("Bifocal +2.00", 0.00m, add: 2.00m, lensType: Bifocal),
            Lens("Progressive +2.00", 0.00m, add: 2.00m, lensType: Progressive),
            Lens("+1.00", 1.00m),
        };

        var forwards = LensSetLenses.InDisplayOrder(lenses, LensTypes).Select(l => l.Id);
        var backwards = LensSetLenses.InDisplayOrder(lenses.Reverse(), LensTypes).Select(l => l.Id);

        Assert.Equal(forwards, backwards);
    }

    [Fact]
    public void Match_SamePowerAndLensType_ReturnsThatLens()
    {
        var plus250 = Lens("+2.50", 2.50m);
        var astigmatic = Lens("-1.00 cyl", -1.00m, cylinder: -0.75m, axis: 90m);
        var bifocal = Lens("Bifocal +2.00", 0.50m, add: 2.00m, lensType: Bifocal);
        var set = new[] { plus250, astigmatic, bifocal };

        Assert.Same(plus250, LensSetLenses.Match(set, sphere: 2.50m, cylinder: null, axis: null, add: null, lensTypeRefId: null));
        Assert.Same(astigmatic, LensSetLenses.Match(set, sphere: -1.00m, cylinder: -0.75m, axis: 90m, add: null, lensTypeRefId: null));
        Assert.Same(bifocal, LensSetLenses.Match(set, sphere: 0.50m, cylinder: null, axis: null, add: 2.00m, lensTypeRefId: Bifocal));
    }

    [Fact]
    public void Match_NoLensWithThatPowerAndLensType_IsNull()
    {
        var set = new[]
        {
            Lens("+2.50", 2.50m),
            Lens("-1.00 cyl", -1.00m, cylinder: -0.75m, axis: 90m),
            Lens("Bifocal +2.00", 0.50m, add: 2.00m, lensType: Bifocal),
        };

        // A different sphere, cylinder, axis and add each miss; so does the right power with the
        // wrong lens type, and a single vision power asked for with a lens type.
        Assert.Null(LensSetLenses.Match(set, 2.75m, null, null, null, null));
        Assert.Null(LensSetLenses.Match(set, -1.00m, -1.00m, 90m, null, null));
        Assert.Null(LensSetLenses.Match(set, -1.00m, -0.75m, 180m, null, null));
        Assert.Null(LensSetLenses.Match(set, 0.50m, null, null, 2.25m, Bifocal));
        Assert.Null(LensSetLenses.Match(set, 0.50m, null, null, 2.00m, Progressive));
        Assert.Null(LensSetLenses.Match(set, 2.50m, null, null, null, Bifocal));
        Assert.Null(LensSetLenses.Match(set, sphere: null, null, null, null, null));
        Assert.Null(LensSetLenses.Match([], 2.50m, null, null, null, null));
    }

    [Fact]
    public void Match_TwoLensesThatDifferOnlyByLensType_ReturnsTheOneWithTheAskedForType()
    {
        var bifocal = Lens("Bifocal +2.00", 0.00m, add: 2.00m, lensType: Bifocal);
        var progressive = Lens("Progressive +2.00", 0.00m, add: 2.00m, lensType: Progressive);
        var set = new[] { bifocal, progressive };

        Assert.Same(bifocal, LensSetLenses.Match(set, 0.00m, null, null, 2.00m, Bifocal));
        Assert.Same(progressive, LensSetLenses.Match(set, 0.00m, null, null, 2.00m, Progressive));
    }

    [Fact]
    public void Match_ComparesPowersTheWayTheRulesDo_AZeroAddIsNoAdd_AndABlankCylinderIsZero()
    {
        var plus250 = Lens("+2.50", 2.50m);
        var storedWithZeros = Lens("+3.00", 3.00m, add: 0.00m, cylinder: 0.00m);
        var set = new[] { plus250, storedWithZeros };

        // A record that sends 0.00 for no add or no cylinder is the same lens as one that sends
        // nothing, in either direction.
        Assert.Same(plus250, LensSetLenses.Match(set, 2.50m, cylinder: 0.00m, axis: null, add: 0.00m, lensTypeRefId: null));
        Assert.Same(storedWithZeros, LensSetLenses.Match(set, 3.00m, cylinder: null, axis: null, add: null, lensTypeRefId: null));
        // Trailing zeros are not a different power.
        Assert.Same(plus250, LensSetLenses.Match(set, 2.500m, null, null, null, null));
    }

    [Fact]
    public void CoatingsFor_OffersOnlyTheCoatingsBothLensesComeIn_InTheCoatingListsOrder()
    {
        var left = Lens("+2.50", 2.50m, coatings: [Photochromic, Clear, BlueBlock]);
        var right = Lens("+2.75", 2.75m, coatings: [BlueBlock, AntiGlare, Photochromic]);

        Assert.Equal([Photochromic, BlueBlock], LensSetLenses.CoatingsFor(left, right).Offered);
        Assert.Equal([Photochromic, Clear, BlueBlock], LensSetLenses.CoatingsFor(left, left).Offered);
        Assert.Empty(LensSetLenses.CoatingsFor(left, Lens("+3.00", 3.00m, coatings: [AntiGlare])).Offered);
    }

    [Fact]
    public void CoatingsFor_RequiresEveryPairingFromEitherLens_EachOnce()
    {
        var blueBlockNeedsPhotochromic = new CoatingPairingRule(BlueBlock, Photochromic);
        var clearNeedsAntiGlare = new CoatingPairingRule(Clear, AntiGlare);
        var left = Lens("+2.50", 2.50m, coatings: [BlueBlock, Photochromic, Clear, AntiGlare], pairings: [blueBlockNeedsPhotochromic]);
        var right = Lens("+2.75", 2.75m, coatings: [BlueBlock, Photochromic, Clear, AntiGlare], pairings: [clearNeedsAntiGlare, blueBlockNeedsPhotochromic]);
        var withoutPairings = Lens("+3.00", 3.00m, coatings: [BlueBlock, Photochromic]);

        Assert.Equal([blueBlockNeedsPhotochromic, clearNeedsAntiGlare], LensSetLenses.CoatingsFor(left, right).RequiredPairings);
        Assert.Equal([clearNeedsAntiGlare, blueBlockNeedsPhotochromic], LensSetLenses.CoatingsFor(withoutPairings, right).RequiredPairings);
        Assert.Equal([blueBlockNeedsPhotochromic], LensSetLenses.CoatingsFor(withoutPairings, left).RequiredPairings);
        Assert.Empty(LensSetLenses.CoatingsFor(withoutPairings, withoutPairings).RequiredPairings);
    }
}

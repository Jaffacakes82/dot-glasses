using DotGlasses.Rules.LensSets;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests.LensSets;

/// <summary>
/// What a coating pairing ticks and locks — the one definition the Field App's coating picker and
/// the Admin Portal's lead conversion (server-rendered and through its script) both apply.
/// </summary>
public class PairedCoatingsTests
{
    private static readonly Guid BlueBlock = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Photochromic = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Clear = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid AntiGlare = Guid.Parse("cccccccc-0000-0000-0000-000000000004");

    [Fact]
    public void TickingATrigger_BringsItsPairedCoating_ThroughAChain()
    {
        IReadOnlyList<CoatingPairingRule> pairings = [new(Clear, AntiGlare), new(AntiGlare, Photochromic)];

        var ticked = PairedCoatings.WithPairedCoatings([Clear], offered: null, pairings);

        Assert.Equal([Clear, AntiGlare, Photochromic], ticked);
    }

    [Fact]
    public void OnlyAnOfferedCoatingIsBrought()
    {
        IReadOnlyList<CoatingPairingRule> pairings = [new(BlueBlock, Photochromic)];

        Assert.Equal([BlueBlock], PairedCoatings.WithPairedCoatings([BlueBlock], offered: [BlueBlock, Clear], pairings));
    }

    [Fact]
    public void APairedCoatingIsLockedByItsTickedTrigger_AndFreedWhenTheTriggerIsUnticked()
    {
        IReadOnlyList<CoatingPairingRule> pairings = [new(BlueBlock, Photochromic)];

        Assert.Equal(BlueBlock, PairedCoatings.LockedBy(Photochromic, [BlueBlock, Photochromic], pairings));
        Assert.Null(PairedCoatings.LockedBy(Photochromic, [Photochromic], pairings));
        Assert.Null(PairedCoatings.LockedBy(BlueBlock, [BlueBlock, Photochromic], pairings));
    }

    [Fact]
    public void APairingThatRunsBothWays_BringsButNeverLocks()
    {
        // Each would hold the other ticked for ever — the Admin Portal used to lock both.
        IReadOnlyList<CoatingPairingRule> pairings = [new(BlueBlock, Photochromic), new(Photochromic, BlueBlock)];

        Assert.Equal([BlueBlock, Photochromic], PairedCoatings.WithPairedCoatings([BlueBlock], offered: null, pairings));
        Assert.Null(PairedCoatings.LockedBy(Photochromic, [BlueBlock, Photochromic], pairings));
        Assert.Null(PairedCoatings.LockedBy(BlueBlock, [BlueBlock, Photochromic], pairings));
        Assert.Empty(PairedCoatings.LockedByTicking(BlueBlock, pairings));
    }

    [Fact]
    public void LockedByTicking_IsEveryOneWayPairingFromTheTrigger()
    {
        IReadOnlyList<CoatingPairingRule> pairings =
        [
            new(Clear, AntiGlare),
            new(Clear, Photochromic),
            new(Photochromic, Clear), // runs both ways with the one above
            new(BlueBlock, Clear),
        ];

        Assert.Equal([AntiGlare], PairedCoatings.LockedByTicking(Clear, pairings));
        Assert.Equal([Clear], PairedCoatings.LockedByTicking(BlueBlock, pairings));
    }
}

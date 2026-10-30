using DotGlasses.Rules.LensSets;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests.LensSets;

/// <summary>
/// What happens to a coating selection when the lens choice changes — the one definition the Field
/// App's form and the Admin Portal's lead conversion both apply.
/// </summary>
public class CoatingSelectionTests
{
    private static readonly Guid BlueBlock = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Photochromic = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Clear = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static readonly IReadOnlyList<CoatingPairingRule> NoPairings = [];

    [Fact]
    public void WhenACoatingSetIsAskedFor_TheOnlyOfferedCoatingIsTicked()
    {
        var reconciled = CoatingSelection.Reconcile([], offered: [Photochromic], NoPairings, asksForCoatingSet: true);

        Assert.Equal([Photochromic], reconciled.Selected);
        Assert.Empty(reconciled.Removed);
    }

    [Fact]
    public void WhenOnlyAPreferenceIsAskedFor_NothingIsTicked()
    {
        var reconciled = CoatingSelection.Reconcile([], offered: [Photochromic], NoPairings, asksForCoatingSet: false);

        Assert.Empty(reconciled.Selected);
    }

    [Fact]
    public void WithTwoOrMoreOffered_TheTicksAreLeftAsTheyAre()
    {
        Assert.Empty(CoatingSelection.Reconcile([], offered: [BlueBlock, Clear], NoPairings, asksForCoatingSet: true).Selected);
        Assert.Equal([Clear], CoatingSelection.Reconcile([Clear], offered: [BlueBlock, Clear], NoPairings, asksForCoatingSet: true).Selected);
    }

    [Fact]
    public void MovingToALensWithOneCoating_UnticksTheOthersAndTicksThatOne()
    {
        var reconciled = CoatingSelection.Reconcile([BlueBlock], offered: [Photochromic], NoPairings, asksForCoatingSet: true);

        Assert.Equal([Photochromic], reconciled.Selected);
        Assert.Equal([BlueBlock], reconciled.Removed);
    }

    [Fact]
    public void AnAlreadyTickedOnlyCoating_IsNotTickedTwice()
    {
        Assert.Equal([Photochromic], CoatingSelection.Reconcile([Photochromic], offered: [Photochromic], NoPairings, asksForCoatingSet: true).Selected);
    }

    [Fact]
    public void TicksStillOffered_AreKept_AndATriggersPairedCoatingIsTicked()
    {
        IReadOnlyList<CoatingPairingRule> pairings = [new(BlueBlock, Photochromic)];

        var reconciled = CoatingSelection.Reconcile([BlueBlock, Clear], offered: [BlueBlock, Photochromic], pairings, asksForCoatingSet: true);

        Assert.Equal([BlueBlock, Photochromic], reconciled.Selected);
        Assert.Equal([Clear], reconciled.Removed);
    }

    [Fact]
    public void AutoTicked_IsTheOnlyOfferedCoating_OrNone()
    {
        Assert.Equal(Photochromic, CoatingSelection.AutoTicked([Photochromic]));
        Assert.Null(CoatingSelection.AutoTicked([BlueBlock, Photochromic]));
        Assert.Null(CoatingSelection.AutoTicked([]));
    }
}

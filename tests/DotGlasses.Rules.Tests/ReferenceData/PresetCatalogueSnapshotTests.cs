using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests.ReferenceData;

/// <summary>
/// The catalogue half of the snapshot — lens rosters, per-lens Coating availability, and the
/// Coating pairing/exclusion rules. Ticket 11's Coating set rules read all of these, so both
/// fillings have to carry them identically.
/// </summary>
public class PresetCatalogueSnapshotTests
{
    private static readonly Guid Catalogue = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OtherCatalogue = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid LensPlus250 = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid LensWithNoCoatings = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid UnknownLens = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000ffff");
    private static readonly Guid BlueBlock = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Photochromic = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Clear = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private static ReferenceDataSnapshot ServerSnapshot() => new(
        [],
        [
            new PresetCatalogueSnapshot(Catalogue, "Six lens set", PresetCatalogueKind.SixLensSet,
            [
                new LensOptionSnapshot(LensPlus250, "+2.50", 0, [BlueBlock, Photochromic]),
                new LensOptionSnapshot(LensWithNoCoatings, "+3.00", 1, []),
            ]),
            new PresetCatalogueSnapshot(OtherCatalogue, "Nine lens set", PresetCatalogueKind.NineLensSet, []),
        ],
        [new CoatingPairingRule(BlueBlock, Photochromic)],
        [new CoatingExclusionRule(Clear, Photochromic)]);

    /// <summary>The same catalogue as the Field App receives it — LensOptionDto already ships
    /// AvailableCoatingIds, which is why no API change was needed for the snapshot.</summary>
    private static ReferenceDataSnapshot ClientSnapshot() => ReferenceDataSnapshot.FromCachedReferenceData(
        [],
        [
            new PresetCatalogueDto
            {
                Id = Catalogue,
                Name = "Six lens set",
                Kind = PresetCatalogueKind.SixLensSet,
                LensOptions =
                [
                    new LensOptionDto { Id = LensPlus250, Label = "+2.50", SortOrder = 0, AvailableCoatingIds = [BlueBlock, Photochromic] },
                    new LensOptionDto { Id = LensWithNoCoatings, Label = "+3.00", SortOrder = 1, AvailableCoatingIds = [] },
                ],
            },
            new PresetCatalogueDto { Id = OtherCatalogue, Name = "Nine lens set", Kind = PresetCatalogueKind.NineLensSet, LensOptions = [] },
        ],
        [new CoatingPairingDto { Id = Guid.NewGuid(), TriggerCoatingRefId = BlueBlock, PairedCoatingRefId = Photochromic }],
        [new CoatingExclusionDto { Id = Guid.NewGuid(), CoatingRefIdA = Clear, CoatingRefIdB = Photochromic }]);

    public static TheoryData<ReferenceDataSnapshot> BothFillings() => new(ServerSnapshot(), ClientSnapshot());

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void ResolveLensOptionLabel_KnownLens_ReturnsTheLensStrengthLabel(ReferenceDataSnapshot snapshot)
    {
        Assert.Equal("+2.50", snapshot.ResolveLensOptionLabel(LensPlus250));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void ResolveLensOptionLabel_UnknownOrNullLens_FallsBackToTheEmDash(ReferenceDataSnapshot snapshot)
    {
        Assert.Equal("—", snapshot.ResolveLensOptionLabel(UnknownLens));
        Assert.Equal("—", snapshot.ResolveLensOptionLabel(null));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void IsCoatingAvailableForLensOption_BothFillings_CarryPerLensAvailability(ReferenceDataSnapshot snapshot)
    {
        Assert.True(snapshot.IsCoatingAvailableForLensOption(LensPlus250, BlueBlock));
        Assert.False(snapshot.IsCoatingAvailableForLensOption(LensPlus250, Clear));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void IsCoatingAvailableForLensOption_LensWithNoCoatingsConfigured_IsFalseNotAnException(ReferenceDataSnapshot snapshot)
    {
        // A real interim state for most non-bifocal strengths — ticket 11 reports this against
        // the lens rather than the Coating set, so it needs an answer, not a throw.
        Assert.False(snapshot.IsCoatingAvailableForLensOption(LensWithNoCoatings, BlueBlock));
        Assert.False(snapshot.IsCoatingAvailableForLensOption(UnknownLens, BlueBlock));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void AreCoatingsExcluded_IsSymmetric(ReferenceDataSnapshot snapshot)
    {
        Assert.True(snapshot.AreCoatingsExcluded(Clear, Photochromic));
        Assert.True(snapshot.AreCoatingsExcluded(Photochromic, Clear));
        Assert.False(snapshot.AreCoatingsExcluded(BlueBlock, Photochromic));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void PairedCoatingsFor_IsDirectional(ReferenceDataSnapshot snapshot)
    {
        Assert.Equal([Photochromic], snapshot.PairedCoatingsFor(BlueBlock));
        Assert.Empty(snapshot.PairedCoatingsFor(Photochromic));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void LensOptionBelongsToCatalogue_OnlyForItsOwnCatalogue(ReferenceDataSnapshot snapshot)
    {
        Assert.True(snapshot.LensOptionBelongsToCatalogue(LensPlus250, Catalogue));
        Assert.False(snapshot.LensOptionBelongsToCatalogue(LensPlus250, OtherCatalogue));
        Assert.False(snapshot.LensOptionBelongsToCatalogue(UnknownLens, Catalogue));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void FindCatalogue_ResolvesNameAndKind(ReferenceDataSnapshot snapshot)
    {
        var catalogue = snapshot.FindCatalogue(Catalogue);

        Assert.NotNull(catalogue);
        Assert.Equal("Six lens set", catalogue.Name);
        Assert.Equal(PresetCatalogueKind.SixLensSet, catalogue.Kind);
        Assert.Equal(2, catalogue.LensOptions.Count);
        Assert.Null(snapshot.FindCatalogue(null));
    }
}

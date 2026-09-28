using System.Text.Json;
using DotGlasses.Contracts.PresetCatalogues;
using DotGlasses.Contracts.ReferenceData;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.Tests.ReferenceData;

/// <summary>
/// The catalogue half of the snapshot — each lens set lens's power, lens type, coatings and own
/// pairings (ADR-0007), and the global coating exclusions. The consultation rules read all of
/// these, so both fillings have to carry them identically.
/// </summary>
public class PresetCatalogueSnapshotTests
{
    private static readonly Guid Catalogue = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OtherCatalogue = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid LensPlus250 = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid LensWithNoCoatings = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid LensBifocal = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");
    private static readonly Guid UnknownLens = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000ffff");
    private static readonly Guid BlueBlock = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid Photochromic = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Clear = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid Bifocal = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

    private static ReferenceDataSnapshot ServerSnapshot() => new(
        [],
        [
            new PresetCatalogueSnapshot(Catalogue, "Six lens set", IsActive: true,
            [
                new LensOptionSnapshot(LensPlus250, "+2.50", 2.50m, [BlueBlock, Photochromic],
                    Pairings: [new CoatingPairingRule(BlueBlock, Photochromic)]),
                new LensOptionSnapshot(LensWithNoCoatings, "+3.00", 3.00m, []),
                new LensOptionSnapshot(LensBifocal, "Bifocal +2.00", 0.00m, [Photochromic],
                    Cylinder: -0.75m, Axis: 90m, Add: 2.00m, LensTypeRefId: Bifocal),
            ], AssignedOrgPaths: null),
            new PresetCatalogueSnapshot(OtherCatalogue, "Nine lens set", IsActive: true, [], AssignedOrgPaths: null),
        ],
        [new CoatingExclusionRule(Clear, Photochromic)]);

    /// <summary>The same catalogue as the Field App receives it — the lens-set DTO carries each
    /// lens's power, lens type, coatings and pairings.</summary>
    private static ReferenceDataSnapshot ClientSnapshot() => ReferenceDataSnapshot.FromCachedReferenceData(
        [],
        [
            new PresetCatalogueDto
            {
                Id = Catalogue,
                Name = "Six lens set",
                LensOptions =
                [
                    new LensOptionDto
                    {
                        Id = LensPlus250, Label = "+2.50", Sphere = 2.50m, CoatingIds = [BlueBlock, Photochromic],
                        Pairings = [new LensCoatingPairingDto { TriggerCoatingRefId = BlueBlock, PairedCoatingRefId = Photochromic }],
                    },
                    new LensOptionDto { Id = LensWithNoCoatings, Label = "+3.00", Sphere = 3.00m, CoatingIds = [] },
                    new LensOptionDto
                    {
                        Id = LensBifocal, Label = "Bifocal +2.00", Sphere = 0.00m, Cylinder = -0.75m, Axis = 90m, Add = 2.00m,
                        LensTypeRefId = Bifocal, CoatingIds = [Photochromic],
                    },
                ],
            },
            new PresetCatalogueDto { Id = OtherCatalogue, Name = "Nine lens set", LensOptions = [] },
        ],
        [new CoatingExclusionDto { Id = Guid.NewGuid(), CoatingRefIdA = Clear, CoatingRefIdB = Photochromic }]);

    public static TheoryData<ReferenceDataSnapshot> BothFillings() => new(ServerSnapshot(), ClientSnapshot());

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void ResolveLensOptionLabel_KnownLens_ReturnsItsTypedLabel(ReferenceDataSnapshot snapshot)
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
    public void BothFillings_CarryEachLensItsPowerAndLensType(ReferenceDataSnapshot snapshot)
    {
        var bifocal = snapshot.FindLensOption(LensBifocal);

        Assert.NotNull(bifocal);
        Assert.Equal((0.00m, -0.75m, 90m, 2.00m), (bifocal.Sphere, bifocal.Cylinder, bifocal.Axis, bifocal.Add));
        Assert.Equal(Bifocal, bifocal.LensTypeRefId);

        var singleVision = snapshot.FindLensOption(LensPlus250)!;
        Assert.Equal((2.50m, (decimal?)null, (decimal?)null, (decimal?)null), (singleVision.Sphere, singleVision.Cylinder, singleVision.Axis, singleVision.Add));
        Assert.Null(singleVision.LensTypeRefId);
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void IsCoatingAvailableForLensOption_ReadsTheLenssOwnCoatings(ReferenceDataSnapshot snapshot)
    {
        Assert.True(snapshot.IsCoatingAvailableForLensOption(LensPlus250, BlueBlock));
        Assert.False(snapshot.IsCoatingAvailableForLensOption(LensPlus250, Clear));

        // The same coating, a different lens: availability follows the lens, not a label shared
        // across sets (the old global grid).
        Assert.True(snapshot.IsCoatingAvailableForLensOption(LensBifocal, Photochromic));
        Assert.False(snapshot.IsCoatingAvailableForLensOption(LensBifocal, BlueBlock));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void IsCoatingAvailableForLensOption_LensWithNoCoatings_IsFalseNotAnException(ReferenceDataSnapshot snapshot)
    {
        // The consultation rules report this against the lens rather than the Coating set, so it
        // needs an answer, not a throw.
        Assert.False(snapshot.IsCoatingAvailableForLensOption(LensWithNoCoatings, BlueBlock));
        Assert.False(snapshot.IsCoatingAvailableForLensOption(UnknownLens, BlueBlock));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void BothFillings_CarryEachLensItsOwnPairings(ReferenceDataSnapshot snapshot)
    {
        Assert.Equal(new[] { new CoatingPairingRule(BlueBlock, Photochromic) }, snapshot.FindLensOption(LensPlus250)!.Pairings);
        Assert.Empty(snapshot.FindLensOption(LensBifocal)!.Pairings);
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
    public void LensOptionBelongsToCatalogue_OnlyForItsOwnCatalogue(ReferenceDataSnapshot snapshot)
    {
        Assert.True(snapshot.LensOptionBelongsToCatalogue(LensPlus250, Catalogue));
        Assert.False(snapshot.LensOptionBelongsToCatalogue(LensPlus250, OtherCatalogue));
        Assert.False(snapshot.LensOptionBelongsToCatalogue(UnknownLens, Catalogue));
    }

    [Theory]
    [MemberData(nameof(BothFillings))]
    public void FindCatalogue_ResolvesNameAndLensOptions(ReferenceDataSnapshot snapshot)
    {
        var catalogue = snapshot.FindCatalogue(Catalogue);

        Assert.NotNull(catalogue);
        Assert.Equal("Six lens set", catalogue.Name);
        Assert.Equal(3, catalogue.LensOptions.Count);
        Assert.Null(snapshot.FindCatalogue(null));
    }

    /// <summary>
    /// A device's IndexedDB cache outlives a release: a lens set cached before ADR-0007 carries
    /// <c>sortOrder</c> and <c>availableCoatingIds</c> where the lens-power shape has neither, and
    /// may spell a collection out as null. Reading it must not throw — the Field App builds this
    /// snapshot for every form, offline included — and the lens comes back with its label and no
    /// coatings until the next online load replaces the cache (the sets it names were all retired
    /// by the reset anyway).
    /// </summary>
    [Fact]
    public void ALensSetCachedBeforeTheLensPowerShape_StillLoads()
    {
        const string cachedBeforeAdr0007 =
            """
            [
              { "id": "aaaaaaaa-0000-0000-0000-000000000001", "name": "6-Lens Set", "lensOptions": [
                { "id": "bbbbbbbb-0000-0000-0000-000000000001", "label": "+2.50", "sortOrder": 0,
                  "availableCoatingIds": ["cccccccc-0000-0000-0000-000000000002"] },
                { "id": "bbbbbbbb-0000-0000-0000-000000000002", "label": "+3.00", "sortOrder": 1,
                  "availableCoatingIds": [], "coatingIds": null, "pairings": null } ] },
              { "id": "aaaaaaaa-0000-0000-0000-000000000002", "name": "9-Lens Set", "lensOptions": null }
            ]
            """;

        var catalogues = JsonSerializer.Deserialize<List<PresetCatalogueDto>>(cachedBeforeAdr0007, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var snapshot = ReferenceDataSnapshot.FromCachedReferenceData([], catalogues, []);

        Assert.Equal("+2.50", snapshot.ResolveLensOptionLabel(LensPlus250));
        Assert.Empty(snapshot.FindLensOption(LensPlus250)!.CoatingIds);
        Assert.Empty(snapshot.FindLensOption(LensWithNoCoatings)!.Pairings);
        Assert.Empty(snapshot.FindCatalogue(OtherCatalogue)!.LensOptions);
    }
}

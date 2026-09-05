using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Tests.Hierarchy;

public class OrgTreeLookupTests
{
    // One tree, shaped to carry every case that matters:
    //
    //   /1/            Dgi          DGI
    //   /1/2/          Country      Kenya
    //   /1/2/3/        Intermediate Nairobi Distributors
    //   /1/2/3/4/      Intermediate Westlands Sub-reseller
    //   /1/2/3/4/5/    RetailPoint  Westlands Optics
    //   /1/2/6/        RetailPoint  Kenya Direct Outlet      <- no Retailer above it
    //   /1/2/30/       Intermediate Thirty Distributors      <- "/1/2/3" is a prefix of "/1/2/30/"
    //   /1/2/30/31/    RetailPoint  Thirty Outlet
    //   /1/2/7/        Intermediate Training Reseller        <- IsTrainingOrg
    //   /1/2/7/8/      RetailPoint  Training Outlet
    private static readonly OrganisationNodeSummary Dgi = Node("DGI", OrganisationLevel.Dgi, "/1/");
    private static readonly OrganisationNodeSummary Kenya = Node("Kenya", OrganisationLevel.Country, "/1/2/");
    private static readonly OrganisationNodeSummary NairobiDistributors = Node("Nairobi Distributors", OrganisationLevel.Intermediate, "/1/2/3/");
    private static readonly OrganisationNodeSummary WestlandsSubReseller = Node("Westlands Sub-reseller", OrganisationLevel.Intermediate, "/1/2/3/4/");
    private static readonly OrganisationNodeSummary WestlandsOptics = Node("Westlands Optics", OrganisationLevel.RetailPoint, "/1/2/3/4/5/");
    private static readonly OrganisationNodeSummary KenyaDirectOutlet = Node("Kenya Direct Outlet", OrganisationLevel.RetailPoint, "/1/2/6/");
    private static readonly OrganisationNodeSummary ThirtyDistributors = Node("Thirty Distributors", OrganisationLevel.Intermediate, "/1/2/30/");
    private static readonly OrganisationNodeSummary ThirtyOutlet = Node("Thirty Outlet", OrganisationLevel.RetailPoint, "/1/2/30/31/");
    private static readonly OrganisationNodeSummary TrainingReseller = Node("Training Reseller", OrganisationLevel.Intermediate, "/1/2/7/", isTrainingOrg: true);
    private static readonly OrganisationNodeSummary TrainingOutlet = Node("Training Outlet", OrganisationLevel.RetailPoint, "/1/2/7/8/");

    private static OrgTreeLookup WholeTree() => new(
    [
        Dgi, Kenya, NairobiDistributors, WestlandsSubReseller, WestlandsOptics,
        KenyaDirectOutlet, ThirtyDistributors, ThirtyOutlet, TrainingReseller, TrainingOutlet,
    ]);

    private static OrganisationNodeSummary Node(string name, OrganisationLevel level, string path, bool isTrainingOrg = false) =>
        new(Guid.NewGuid(), name, level, path, isTrainingOrg);

    private static HierarchyPath Path(string value) => HierarchyPath.Parse(value);

    [Fact]
    public void ARowResolvesToTheNameOfTheNodeItSitsOn()
    {
        Assert.Equal("Westlands Optics", WholeTree().OutletName(Path("/1/2/3/4/5/")));
    }

    [Fact]
    public void AnOutletIsAnExactMatchNotTheNearestAncestor()
    {
        // A path one level below a known outlet is not that outlet — it is unknown.
        Assert.Equal(OrgTreeLookup.UnknownOutlet, WholeTree().OutletName(Path("/1/2/3/4/5/99/")));
    }

    [Fact]
    public void ACountryIsResolvedForARowManyLevelsBeneathIt()
    {
        // The case that made this an ancestor lookup rather than a parent lookup: the row sits
        // three levels below its country.
        Assert.Equal("Kenya", WholeTree().CountryName(Path("/1/2/3/4/5/")));
    }

    [Fact]
    public void ATreeMissingTheAncestorsReportsUnknownRatherThanGuessing()
    {
        // What a plain scoped OrganisationNodes query hands you when the caller sits below Country
        // level: their own subtree only, with the country invisible above them. The lookup cannot
        // detect the wrong feed, so it must report the gap honestly — this is why callers go
        // through IUnscopedReportQueryService (CLAUDE.md).
        var scopedToTheSubtreeOnly = new OrgTreeLookup([WestlandsSubReseller, WestlandsOptics]);

        Assert.Equal(OrgTreeLookup.UnknownCountry, scopedToTheSubtreeOnly.CountryName(Path("/1/2/3/4/5/")));
        Assert.Equal("Westlands Optics", scopedToTheSubtreeOnly.OutletName(Path("/1/2/3/4/5/")));
    }

    [Fact]
    public void AnUnknownPathReportsUnknownRatherThanEmptyNames()
    {
        var lookup = WholeTree();
        var stranger = Path("/9/99/");

        Assert.Equal(OrgTreeLookup.UnknownOutlet, lookup.OutletName(stranger));
        Assert.Equal(OrgTreeLookup.UnknownCountry, lookup.CountryName(stranger));
        Assert.Equal(OrgTreeLookup.UnknownRetailer, lookup.RetailerName(stranger));
        Assert.Equal(RetailerResolutionKind.UnknownOrganisation, lookup.ResolveRetailer(stranger).Kind);
    }

    [Fact]
    public void TheRetailerIsTheNearestIntermediateAncestorNotTheHighestOne()
    {
        var resolution = WholeTree().ResolveRetailer(Path("/1/2/3/4/5/"));

        Assert.True(resolution.HasRetailer);
        Assert.Equal("Westlands Sub-reseller", resolution.Name);
        Assert.Equal(WestlandsSubReseller.Id, resolution.Node!.Id);
    }

    [Fact]
    public void ARetailPointDirectlyUnderACountryHasNoRetailer()
    {
        // CONTEXT.md: reporting says so rather than substituting the country.
        var lookup = WholeTree();
        var resolution = lookup.ResolveRetailer(Path("/1/2/6/"));

        Assert.False(resolution.HasRetailer);
        Assert.Equal(RetailerResolutionKind.NoRetailer, resolution.Kind);
        Assert.Null(resolution.Node);
        Assert.Equal(OrgTreeLookup.NoRetailer, lookup.RetailerName(Path("/1/2/6/")));
        Assert.NotEqual("Kenya", lookup.RetailerName(Path("/1/2/6/")));
    }

    [Fact]
    public void HavingNoRetailerIsDistinctFromNotBeingInTheTree()
    {
        var lookup = WholeTree();

        Assert.Equal(RetailerResolutionKind.NoRetailer, lookup.ResolveRetailer(Path("/1/2/6/")).Kind);
        Assert.Equal(RetailerResolutionKind.UnknownOrganisation, lookup.ResolveRetailer(Path("/1/2/99/")).Kind);
    }

    [Fact]
    public void ARetailPointIsNotAttributedToARetailerItMerelySharesLeadingDigitsWith()
    {
        // "/1/2/3/" is a character prefix of "/1/2/30/31/" but an unrelated branch of the tree.
        var resolution = WholeTree().ResolveRetailer(Path("/1/2/30/31/"));

        Assert.Equal("Thirty Distributors", resolution.Name);
    }

    [Fact]
    public void ACountryIsNotAttributedToARowItMerelySharesLeadingDigitsWith()
    {
        var lookup = new OrgTreeLookup([Node("Four", OrganisationLevel.Country, "/1/4/"), Node("Forty", OrganisationLevel.Country, "/1/40/")]);

        Assert.Equal("Forty", lookup.CountryName(Path("/1/40/41/")));
    }

    [Fact]
    public void AnIntermediateNodeIsItsOwnRetailer()
    {
        // Aggregates keyed by Retailer must place a row recorded at the reseller itself under that
        // reseller, not under whatever sits above it.
        Assert.Equal("Nairobi Distributors", WholeTree().RetailerName(Path("/1/2/3/")));
    }

    [Fact]
    public void ARowBeneathATrainingOrgIsFlaggedAsTraining()
    {
        var lookup = WholeTree();

        Assert.True(lookup.IsUnderTrainingOrg(Path("/1/2/7/8/")));
        Assert.True(lookup.IsUnderTrainingOrg(Path("/1/2/7/")));
        Assert.False(lookup.IsUnderTrainingOrg(Path("/1/2/3/4/5/")));
    }

    [Fact]
    public void ASiblingOfATrainingOrgIsNotFlaggedAsTraining()
    {
        var lookup = new OrgTreeLookup(
        [
            Node("Training Seven", OrganisationLevel.Intermediate, "/1/2/7/", isTrainingOrg: true),
            Node("Live Seventy", OrganisationLevel.Intermediate, "/1/2/70/"),
        ]);

        Assert.False(lookup.IsUnderTrainingOrg(Path("/1/2/70/71/")));
    }

    [Fact]
    public void ANodeWhoseStoredPathBreaksTheInvariantIsRejectedRatherThanSilentlyIgnored()
    {
        Assert.Throws<ArgumentException>(() => new OrgTreeLookup([Node("Broken", OrganisationLevel.Country, "/1/2")]));
    }
}

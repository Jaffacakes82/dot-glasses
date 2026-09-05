using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Tests.Hierarchy;

/// <summary>
/// OrgTreeLookup is strict: it takes a HierarchyPath, and building one from a malformed string
/// throws. These wrappers are the one place that strictness is relaxed, and only for a persisted
/// row's path column — see OrgTreeLookupRowExtensions for why the two edges want opposite
/// answers. What is pinned here is that the relaxation is exactly "an unreadable path is a path
/// we cannot resolve", with no other behaviour bought or lost: a well-formed path must still get
/// the same answer OrgTreeLookup itself gives, or the reporting screens would quietly disagree
/// with the module they are supposed to share.
/// </summary>
public class OrgTreeLookupRowExtensionsTests
{
    private static readonly OrganisationNodeSummary Kenya = Node("Kenya", OrganisationLevel.Country, "/1/2/");
    private static readonly OrganisationNodeSummary Kangemi = Node("Kangemi Vision Centre", OrganisationLevel.Intermediate, "/1/2/3/");
    private static readonly OrganisationNodeSummary OutreachPost = Node("Outreach Post", OrganisationLevel.RetailPoint, "/1/2/3/4/");
    private static readonly OrganisationNodeSummary TrainingReseller = Node("Training Reseller", OrganisationLevel.Intermediate, "/1/2/7/", isTrainingOrg: true);

    private static OrgTreeLookup Lookup() => new([Kenya, Kangemi, OutreachPost, TrainingReseller]);

    private static OrganisationNodeSummary Node(string name, OrganisationLevel level, string path, bool isTrainingOrg = false) =>
        new(Guid.NewGuid(), name, level, path, isTrainingOrg);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/1/2")]
    [InlineData("1/2/")]
    [InlineData("/1//2/")]
    [InlineData("/1/abc/")]
    public void AnUnreadableRowPathResolvesToUnknownRatherThanBringingTheScreenDown(string? rowPath)
    {
        // IHierarchyScoped.HierarchyPath defaults to "" and is stamped from a claim that is itself
        // absent for a user with no org assignment, so "" in particular is reachable rather than
        // hypothetical. A reporting screen reads every row the caller can see; one bad row must
        // cost that row's names, not the whole page.
        var lookup = Lookup();

        Assert.Equal(OrgTreeLookup.UnknownOutlet, lookup.RowOutletName(rowPath));
        Assert.Equal(OrgTreeLookup.UnknownCountry, lookup.RowCountryName(rowPath));
        Assert.Equal(OrgTreeLookup.UnknownRetailer, lookup.RowRetailerName(rowPath));
        Assert.Null(lookup.RowOutlet(rowPath));
        Assert.Equal(RetailerResolutionKind.UnknownOrganisation, lookup.RowRetailer(rowPath).Kind);
    }

    [Fact]
    public void AnUnreadableRowPathIsNotTreatedAsTraining()
    {
        // Dashboard aggregates exclude training rows, so answering "yes" here would silently drop
        // real data instead of merely failing to name it.
        Assert.False(Lookup().IsRowUnderTrainingOrg("not-a-path"));
    }

    [Fact]
    public void AWellFormedRowPathGetsTheSameAnswerAsTheModuleItself()
    {
        var lookup = Lookup();

        Assert.Equal("Outreach Post", lookup.RowOutletName("/1/2/3/4/"));
        Assert.Equal("Kenya", lookup.RowCountryName("/1/2/3/4/"));
        Assert.Equal("Kangemi Vision Centre", lookup.RowRetailerName("/1/2/3/4/"));
        Assert.Equal(OutreachPost.Id, lookup.RowOutlet("/1/2/3/4/")!.Id);
        Assert.True(lookup.IsRowUnderTrainingOrg("/1/2/7/8/"));
    }

    [Fact]
    public void AKnownRowWithNoRetailerStaysDistinctFromAnUnreadableOne()
    {
        // The distinction the Custom Orders grouping turns on: both lack a Retailer node, but only
        // one of them is a fact about the tree.
        var lookup = new OrgTreeLookup([Kenya, Node("Kenya Direct Outlet", OrganisationLevel.RetailPoint, "/1/2/6/")]);

        Assert.Equal(RetailerResolutionKind.NoRetailer, lookup.RowRetailer("/1/2/6/").Kind);
        Assert.Equal(OrgTreeLookup.NoRetailer, lookup.RowRetailerName("/1/2/6/"));
        Assert.Equal(RetailerResolutionKind.UnknownOrganisation, lookup.RowRetailer("").Kind);
    }
}

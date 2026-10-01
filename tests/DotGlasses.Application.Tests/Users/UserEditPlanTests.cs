using DotGlasses.Application.Organisations;
using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Tests.Users;

public class UserEditPlanTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void NothingChanged_IsAnEmptyPlan()
    {
        var plan = UserEditPlan.Diff("Amina Okoro", " Amina Okoro ", "User", "User", [A, B], [B, A]);

        Assert.True(plan.IsEmpty);
    }

    [Fact]
    public void OnlyTheDifferences_AreInThePlan()
    {
        var plan = UserEditPlan.Diff("Amina Okoro", "Amina O.", "User", "Admin", loadedOrgIds: [A, B], orgIds: [B, C]);

        Assert.Equal("Amina O.", plan.NewFullName);
        Assert.Equal("Admin", plan.NewRole);
        Assert.Equal([C], plan.OrgsToAdd);
        Assert.Equal([A], plan.OrgsToRemove);
    }

    [Fact]
    public void AnAssignmentNeitherLoadedNorSubmitted_IsNotTouched()
    {
        // Someone else added C after the page loaded with A: the save says nothing about C.
        var plan = UserEditPlan.Diff(null, null, "User", "User", loadedOrgIds: [A], orgIds: [A, B]);

        Assert.Equal([B], plan.OrgsToAdd);
        Assert.Empty(plan.OrgsToRemove);
        Assert.DoesNotContain(C, plan.OrgsToAdd.Concat(plan.OrgsToRemove));
    }

    [Theory]
    [InlineData("/1/2/3/4/", "/1/2/", false)] // a retail point beneath a country you keep
    [InlineData("/1/2/", "/1/2/", false)]     // the same organisation, kept through another row
    [InlineData("/1/2/", "/1/2/3/4/", true)]  // the country, keeping only a retail point in it
    [InlineData("/1/2/3/", "/1/5/", true)]    // nothing you keep covers it
    public void RemovingYourOwnAssignment_ShrinksYourScope_UnlessAnotherYouKeepCoversIt(string removed, string kept, bool shrinks)
    {
        Assert.Equal(shrinks, OwnAssignments.RemovalShrinksScope(HierarchyPath.Parse(removed), [HierarchyPath.Parse(kept)]));
    }

    [Fact]
    public void RemovingYourOnlyAssignment_ShrinksYourScope()
    {
        Assert.True(OwnAssignments.RemovalShrinksScope(HierarchyPath.Parse("/1/2/"), []));
    }

    [Theory]
    [InlineData(OrganisationLevel.Dgi, "DGI")]
    [InlineData(OrganisationLevel.Country, "Country")]
    [InlineData(OrganisationLevel.Intermediate, "Retailer/distributor")]
    [InlineData(OrganisationLevel.RetailPoint, "Retail Point")]
    public void ALevel_ReadsAsWords(OrganisationLevel level, string label)
    {
        Assert.Equal(label, OrganisationLevelLabels.For(level));
    }

    [Fact]
    public void ADeactivatedOrganisation_KeepsItsNameInReports_MarkedDeactivated()
    {
        var country = new OrganisationNodeSummary(Guid.NewGuid(), "Kenya", OrganisationLevel.Country, "/1/2/", false);
        var retailer = new OrganisationNodeSummary(Guid.NewGuid(), "Closed Retailer", OrganisationLevel.Intermediate, "/1/2/3/", false, IsDeactivated: true);
        var outlet = new OrganisationNodeSummary(Guid.NewGuid(), "Closed Outlet", OrganisationLevel.RetailPoint, "/1/2/3/4/", false, IsDeactivated: true);
        var lookup = new OrgTreeLookup([country, retailer, outlet]);
        var path = HierarchyPath.Parse("/1/2/3/4/");

        Assert.Equal("Closed Outlet (deactivated)", lookup.OutletName(path));
        Assert.Equal("Closed Retailer (deactivated)", lookup.RetailerName(path));
        Assert.Equal("Kenya", lookup.CountryName(path));
    }
}

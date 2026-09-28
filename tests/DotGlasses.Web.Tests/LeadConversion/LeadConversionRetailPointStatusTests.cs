using System.Net;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.LeadConversion;

/// <summary>
/// The Admin Portal half of ticket 07's "recording" rule: a Lead whose retail point has since
/// been deactivated can't be converted into a Sale there. Unlike the Field App create endpoints
/// (RecordingLocationApiTests), this isn't a JWT current-location check — it's a plain lookup of
/// the org node standing at the Lead's own HierarchyPath, deliberately reading past the
/// soft-delete filter (IOrganisationNodeLookup) precisely because deactivation is the thing being
/// asked about.
/// </summary>
public class LeadConversionRetailPointStatusTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    [Fact]
    public async Task ConvertingALeadWhoseRetailPointHasSinceBeenDeactivated_IsRefusedNamingIt()
    {
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.Customers.Add(new Customer
            {
                Id = customerId,
                FullName = "Wanjiru Kamau",
                PhoneNumber = "+254711000000",
                HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath,
            });
            db.Leads.Add(new Lead
            {
                Id = leadId,
                CustomerId = customerId,
                TechnicianUserId = Guid.NewGuid(),
                HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath,
                ConsentGiven = true,
            });
            db.OrganisationNodes.IgnoreQueryFilters().Single(o => o.Id == OrganisationSeedConfiguration.KenyaRetailPointId).IsDeleted = true;
        });

        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");

        var (redirect, html) = await AdminPortalFactory.PostAndFollowAsync(
            client,
            $"/Leads/Convert/{leadId}",
            AdminPortalFactory.Form(token),
            referer: $"/Leads/Convert/{leadId}");

        Assert.Equal($"/Leads/Convert/{leadId}", redirect.Headers.Location?.ToString());
        // The org name's em dash is HTML-encoded by the view, so this checks around it rather
        // than reproducing the exact byte sequence.
        Assert.Contains("Kangemi Vision Centre", html);
        Assert.Contains("Outreach Post has been deactivated.", html);

        // Nothing was saved — the Lead is still open, not half-converted. IgnoreQueryFilters()
        // because this bare scope has no HttpContext/caller, which the hierarchy filter reads as
        // "no scope paths" and so would hide every row, not just deactivated ones.
        var lead = Query(db => db.Leads.IgnoreQueryFilters().Single(l => l.Id == leadId));
        Assert.False(lead.ConvertedFlag);
        Assert.Null(lead.SaleId);
    }

    /// <summary>
    /// A Lead whose HierarchyPath matches no OrganisationNode at all — no node is "found and
    /// deactivated", so this ground for refusal doesn't apply and conversion proceeds exactly as
    /// it did before ticket 07. This stands in for the two real-world shapes the spec calls out
    /// (a record stamped above retail-point level, and a legacy row with no exact org such as
    /// ""): an outright-empty HierarchyPath isn't reachable here because it wouldn't match the
    /// admin's own scope prefix ("/1/") and so the scoped Lead query would hide it before this
    /// check ever ran — this fabricated-but-in-scope path is the reachable version of the same
    /// "no node found" case.
    /// </summary>
    [Fact]
    public async Task ConvertingALeadWhoseHierarchyPathMatchesNoOrgNode_ProceedsAsBefore()
    {
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        const string noMatchingNodePath = OrganisationSeedConfiguration.KenyaRetailerPath + "999/";
        factory.Seed(db =>
        {
            db.Customers.Add(new Customer
            {
                Id = customerId,
                FullName = "Akinyi Otieno",
                PhoneNumber = "+254711000001",
                HierarchyPath = noMatchingNodePath,
            });
            db.Leads.Add(new Lead
            {
                Id = leadId,
                CustomerId = customerId,
                TechnicianUserId = Guid.NewGuid(),
                HierarchyPath = noMatchingNodePath,
                ConsentGiven = true,
            });
        });

        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");

        var response = await client.PostAsync(
            $"/Leads/Convert/{leadId}",
            AdminPortalFactory.Form(token, ("Form.ConsentGiven", "true")));

        // Not refused on the "retail point deactivated" ground — the form comes back asking for a
        // lens range instead (the Lead recorded none), which is the same 200-with-validation shape
        // LeadConversionLensSetTests.SubmittingWithNoLensRangeChosenAsksForOne pins for an ordinary
        // in-scope Lead, not a redirect-with-TempData domain rejection.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("has been deactivated.", html);
        Assert.Contains("Choose a lens range.", html);
    }

    private T Query<T>(Func<DotGlassesDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>());
    }
}

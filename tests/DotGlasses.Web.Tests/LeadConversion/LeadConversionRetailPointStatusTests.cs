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

    private T Query<T>(Func<DotGlassesDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>());
    }
}

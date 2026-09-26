using System.Net;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotGlasses.Web.Tests.LeadConversion;

/// <summary>
/// The Admin Portal's Lead→Sale conversion, for a Lead that recorded no lens preference: the
/// admin picks the lens range themselves, from the lens sets reaching the *Lead's* retail point
/// (the Sale inherits the Lead's attribution), then Custom prescription — ADR-0005.
/// </summary>
public class LeadConversionLensSetTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private Guid SeedLeadWithNoLensPreference()
    {
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.Customers.Add(new Customer { Id = customerId, FullName = "Wanjiru Kamau", PhoneNumber = "+254711000000", HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath });
            db.Leads.Add(new Lead { Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, ConsentGiven = true });
        });
        return leadId;
    }

    private T Query<T>(Func<DotGlassesDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>());
    }

    [Fact]
    public async Task TheLensRangeChoiceOffersTheLeadsLensSetsThenCustom()
    {
        var leadId = SeedLeadWithNoLensPreference();
        var client = factory.CreateAdminClient();

        var html = await client.GetStringAsync($"/Leads/Convert/{leadId}");

        var six = html.IndexOf($"value=\"{PresetCatalogueSeedConfiguration.SixLensSetId}\">6-Lens Set<", StringComparison.Ordinal);
        var nine = html.IndexOf($"value=\"{PresetCatalogueSeedConfiguration.NineLensSetId}\">9-Lens Set<", StringComparison.Ordinal);
        var custom = html.IndexOf("value=\"custom\">Custom prescription<", StringComparison.Ordinal);
        Assert.True(six >= 0 && nine > six && custom > nine, "Expected 6-Lens Set, then 9-Lens Set, then Custom prescription as lens range options.");
    }

    [Fact]
    public async Task ChoosingALensSetConvertsTheLeadIntoALensSetSaleOnThatSet()
    {
        var leadId = SeedLeadWithNoLensPreference();
        var (lensOptionId, coatingId) = Query(db =>
            (from option in db.LensOptions
             join availability in db.LensStrengthCoatingOptions on option.LensStrengthRefId equals availability.LensStrengthRefId
             where option.PresetCatalogueId == PresetCatalogueSeedConfiguration.NineLensSetId
             select new { option.Id, availability.CoatingRefId }).AsEnumerable().Select(x => (x.Id, x.CoatingRefId)).First());
        var frameColourId = Query(db => db.ReferenceDataItems.First(x => x.Category == ReferenceDataCategory.FrameColour && x.IsActive && !x.IsOtherOption).Id);

        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");
        var (_, _) = await AdminPortalFactory.PostAndFollowAsync(client, $"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token,
            ("Form.ConsentGiven", "true"),
            ("Form.LensRange", PresetCatalogueSeedConfiguration.NineLensSetId.ToString()),
            ("Form.LensOptionLeftId", lensOptionId.ToString()),
            ("Form.LensOptionRightId", lensOptionId.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.FrameColourRefId", frameColourId.ToString()),
            ("Form.CoatingRefIds", coatingId.ToString())));

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal(LensRangeType.LensSet, sale.LensRangeType);
        Assert.Equal(PresetCatalogueSeedConfiguration.NineLensSetId, sale.PresetCatalogueId);
    }

    [Fact]
    public async Task ALeadWhoseLensSetNoLongerReachesItsRetailPoint_IsNotConverted_AndSaysWhy()
    {
        // The Lead's lens set carries over as-is (SaleAssembly.Seed); nothing swaps in another. The
        // check runs at the *Lead's* location, and this set is assigned nowhere near it.
        var (lensOptionStrength, coatingId) = Query(db =>
            db.LensStrengthCoatingOptions.AsEnumerable().Select(x => (x.LensStrengthRefId, x.CoatingRefId)).First());
        var frameColourId = Query(db => db.ReferenceDataItems.First(x => x.Category == ReferenceDataCategory.FrameColour && x.IsActive && !x.IsOtherOption).Id);
        var lensSetId = Guid.NewGuid();
        var lensOptionId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = lensSetId, Name = $"Unassigned Readers {lensSetId:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            db.LensOptions.Add(new LensOption { Id = lensOptionId, PresetCatalogueId = lensSetId, LensStrengthRefId = lensOptionStrength, SortOrder = 0 });
            db.Customers.Add(new Customer { Id = customerId, FullName = "Otieno Were", PhoneNumber = "+254722000000", HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath });
            db.Leads.Add(new Lead
            {
                Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, ConsentGiven = true,
                LensRangeType = LensRangeType.LensSet, PresetCatalogueId = lensSetId, LensOptionLeftId = lensOptionId, LensOptionRightId = lensOptionId, PresetPupilDistanceBucket = 2,
            });
        });

        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");
        var response = await client.PostAsync($"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token,
            ("Form.ConsentGiven", "true"),
            ("Form.FrameColourRefId", frameColourId.ToString()),
            ("Form.CoatingRefIds", coatingId.ToString())));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This lens set isn&#x27;t available at this retail point", await response.Content.ReadAsStringAsync());
        Assert.False(Query(db => db.Sales.IgnoreQueryFilters().Any(s => s.SourceLeadId == leadId)));
    }

    [Fact]
    public async Task SubmittingWithNoLensRangeChosenAsksForOne()
    {
        var leadId = SeedLeadWithNoLensPreference();
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");

        var response = await client.PostAsync($"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token, ("Form.ConsentGiven", "true")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Choose a lens range.", await response.Content.ReadAsStringAsync());
    }
}

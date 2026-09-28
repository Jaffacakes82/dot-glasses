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

        var six = html.IndexOf($"value=\"{ExampleLensSets.SixLensSetId}\">6-Lens Set<", StringComparison.Ordinal);
        var nine = html.IndexOf($"value=\"{ExampleLensSets.NineLensSetId}\">9-Lens Set<", StringComparison.Ordinal);
        var custom = html.IndexOf("value=\"custom\">Custom prescription<", StringComparison.Ordinal);
        Assert.True(six >= 0 && nine > six && custom > nine, "Expected 6-Lens Set, then 9-Lens Set, then Custom prescription as lens range options.");
    }

    /// <summary>A lens on the example 9-Lens set and a coating it comes in — the minimum a lens-set
    /// Sale needs to pass the rules.</summary>
    private (Guid LensOptionId, Guid CoatingId) NineLensOptionWithACoating() => Query(db =>
        (from option in db.LensOptions
         join coating in db.LensOptionCoatings on option.Id equals coating.LensOptionId
         where option.PresetCatalogueId == ExampleLensSets.NineLensSetId
         select new { option.Id, coating.CoatingRefId }).AsEnumerable().Select(x => (x.Id, x.CoatingRefId)).First());

    private Guid AFrameColour() =>
        Query(db => db.ReferenceDataItems.First(x => x.Category == ReferenceDataCategory.FrameColour && x.IsActive && !x.IsOtherOption).Id);

    private async Task ConvertOntoTheNineLensSetAsync(HttpClient client, Guid leadId)
    {
        var (lensOptionId, coatingId) = NineLensOptionWithACoating();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");
        await AdminPortalFactory.PostAndFollowAsync(client, $"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token,
            ("Form.ConsentGiven", "true"),
            ("Form.LensRange", ExampleLensSets.NineLensSetId.ToString()),
            ("Form.LensOptionLeftId", lensOptionId.ToString()),
            ("Form.LensOptionRightId", lensOptionId.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.FrameColourRefId", AFrameColour().ToString()),
            ("Form.CoatingRefIds", coatingId.ToString())));
    }

    [Fact]
    public async Task ChoosingALensSetConvertsTheLeadIntoALensSetSaleOnThatSet()
    {
        var leadId = SeedLeadWithNoLensPreference();

        await ConvertOntoTheNineLensSetAsync(factory.CreateAdminClient(), leadId);

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal(LensRangeType.LensSet, sale.LensRangeType);
        Assert.Equal(ExampleLensSets.NineLensSetId, sale.PresetCatalogueId);
    }

    [Fact]
    public async Task ALeadWhoseLensSetNoLongerReachesItsRetailPoint_SaysWhy_AndTheAdminPicksAgain()
    {
        // The Lead's lens set carries over as-is (SaleAssembly.Seed), and nothing swaps in another
        // silently — but a read-only summary would leave the admin nowhere to go. So the screen says
        // why and offers the lens range choice instead, checked at the *Lead's* location.
        var lensSetId = Guid.NewGuid();
        var lensOptionId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = lensSetId, Name = $"Unassigned Readers {lensSetId:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            db.LensOptions.Add(new LensOption { Id = lensOptionId, PresetCatalogueId = lensSetId, Label = "+2.50", Sphere = 2.50m });
            db.Customers.Add(new Customer { Id = customerId, FullName = "Otieno Were", PhoneNumber = "+254722000000", HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath });
            db.Leads.Add(new Lead
            {
                Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, ConsentGiven = true,
                LensRangeType = LensRangeType.LensSet, PresetCatalogueId = lensSetId, LensOptionLeftId = lensOptionId, LensOptionRightId = lensOptionId, PresetPupilDistanceBucket = 2,
            });
        });
        var client = factory.CreateAdminClient();

        var page = await client.GetStringAsync($"/Leads/Convert/{leadId}");
        Assert.Contains($"Unassigned Readers {lensSetId:N}", page);
        Assert.Contains("isn't available at this lead's retail point any more", page);
        Assert.Contains("name=\"Form.LensRange\"", page);

        await ConvertOntoTheNineLensSetAsync(client, leadId);

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal(ExampleLensSets.NineLensSetId, sale.PresetCatalogueId);
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

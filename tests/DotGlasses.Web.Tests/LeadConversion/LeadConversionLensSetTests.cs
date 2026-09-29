using System.Net;
using System.Text.RegularExpressions;
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
/// (the Sale inherits the Lead's attribution), then Custom prescription — ADR-0005. A lens-set
/// Sale records the chosen lenses' powers and lens type, never a lens id (ADR-0007).
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

    private sealed record ChosenLens(Guid Id, decimal Sphere, decimal? Add, Guid? LensTypeRefId, Guid CoatingId);

    /// <summary>A lens on the example 9-Lens set and a coating it comes in — the minimum a lens-set
    /// Sale needs to pass the rules.</summary>
    private ChosenLens NineLensOptionWithACoating() => Query(db =>
        (from option in db.LensOptions
         join coating in db.LensOptionCoatings on option.Id equals coating.LensOptionId
         where option.PresetCatalogueId == ExampleLensSets.NineLensSetId
         orderby option.Label
         select new { option.Id, option.Sphere, option.Add, option.LensTypeRefId, coating.CoatingRefId })
        .AsEnumerable().Select(x => new ChosenLens(x.Id, x.Sphere, x.Add, x.LensTypeRefId, x.CoatingRefId)).First());

    private Guid AFrameColour() =>
        Query(db => db.ReferenceDataItems.First(x => x.Category == ReferenceDataCategory.FrameColour && x.IsActive && !x.IsOtherOption).Id);

    private async Task<ChosenLens> ConvertOntoTheNineLensSetAsync(HttpClient client, Guid leadId)
    {
        var lens = NineLensOptionWithACoating();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");
        await AdminPortalFactory.PostAndFollowAsync(client, $"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token,
            ("Form.ConsentGiven", "true"),
            ("Form.LensRange", ExampleLensSets.NineLensSetId.ToString()),
            ("Form.LensLeftId", lens.Id.ToString()),
            ("Form.LensRightId", lens.Id.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.FrameColourRefId", AFrameColour().ToString()),
            ("Form.CoatingRefIds", lens.CoatingId.ToString())));
        return lens;
    }

    [Fact]
    public async Task ChoosingALensSetConvertsTheLeadIntoALensSetSaleRecordingTheLensPowers()
    {
        var leadId = SeedLeadWithNoLensPreference();

        var lens = await ConvertOntoTheNineLensSetAsync(factory.CreateAdminClient(), leadId);

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal(LensRangeType.LensSet, sale.LensRangeType);
        Assert.Equal(ExampleLensSets.NineLensSetId, sale.PresetCatalogueId);
        Assert.Equal((lens.Sphere, lens.Sphere), (sale.SphereLeft!.Value, sale.SphereRight!.Value));
        Assert.Equal((lens.Add, lens.Add), (sale.AddLeft, sale.AddRight));
        Assert.Equal(lens.LensTypeRefId, sale.LensTypeRefId);
    }

    [Fact]
    public async Task ALeadWhoseLensSetNoLongerReachesItsRetailPoint_SaysWhy_AndTheAdminPicksAgain()
    {
        // The Lead's lens set carries over as-is (SaleAssembly.Seed), and nothing swaps in another
        // silently — but a read-only summary would leave the admin nowhere to go. So the screen says
        // why and offers the lens range choice instead, checked at the *Lead's* location.
        var lensSetId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = lensSetId, Name = $"Unassigned Readers {lensSetId:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            db.LensOptions.Add(new LensOption { Id = Guid.NewGuid(), PresetCatalogueId = lensSetId, Label = "+2.50", Sphere = 2.50m });
            db.Customers.Add(new Customer { Id = customerId, FullName = "Otieno Were", PhoneNumber = "+254722000000", HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath });
            db.Leads.Add(new Lead
            {
                Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, ConsentGiven = true,
                LensRangeType = LensRangeType.LensSet, PresetCatalogueId = lensSetId, SphereLeft = 2.50m, SphereRight = 2.50m, PresetPupilDistanceBucket = 2,
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
    public async Task ALeadWhoseLensIsNoLongerInItsLensSet_SaysSo_AndPreselectsTheSetAndWhateverStillMatches()
    {
        // The set still reaches the Lead's retail point, but the right eye's +3.50 has gone from it
        // since the Lead was captured. A record holds no lens id (ADR-0007), so "which lens was it"
        // is answered by power and lens type (LensSetLenses.Match): the left eye's +2.50 still
        // matches and is pre-selected, the right eye is left for the admin to choose.
        var lensSetId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var plus250 = default(Guid);
        var plus300 = default(Guid);
        var coatingId = default(Guid);
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = lensSetId, Name = $"Kenya Readers {lensSetId:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            (plus250, coatingId) = LensSetTestData.AddSellableLens(db, lensSetId);
            (plus300, _) = LensSetTestData.AddSellableLens(db, lensSetId, "+3.00", 3.00m);
            db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSetId, OrgNodeId = OrganisationSeedConfiguration.KenyaRetailerId });
            db.Customers.Add(new Customer { Id = customerId, FullName = "Achieng Odhiambo", PhoneNumber = "+254733000000", HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath });
            db.Leads.Add(new Lead
            {
                Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, ConsentGiven = true,
                LensRangeType = LensRangeType.LensSet, PresetCatalogueId = lensSetId, SphereLeft = LensSetTestData.SellableSphere, SphereRight = 3.50m, PresetPupilDistanceBucket = 2,
            });
        });
        var client = factory.CreateAdminClient();

        var page = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync($"/Leads/Convert/{leadId}"));

        Assert.Contains($"The SPH +3.50 on this Lead is no longer in Kenya Readers {lensSetId:N}. Choose a lens.", page);
        Assert.Equal(lensSetId.ToString(), SelectedValue(page, "Form.LensRange"));
        Assert.Equal(plus250.ToString(), SelectedValue(page, "Form.LensLeftId"));
        Assert.Null(SelectedValue(page, "Form.LensRightId"));

        // The admin picks the right eye's lens; the Sale records both eyes' powers.
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");
        await AdminPortalFactory.PostAndFollowAsync(client, $"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token,
            ("Form.ConsentGiven", "true"),
            ("Form.LensRange", lensSetId.ToString()),
            ("Form.LensLeftId", plus250.ToString()),
            ("Form.LensRightId", plus300.ToString()),
            ("Form.PresetPupilDistanceBucket", "2"),
            ("Form.FrameColourRefId", AFrameColour().ToString()),
            ("Form.CoatingRefIds", coatingId.ToString())));

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal((lensSetId, 2.50m, 3.00m), (sale.PresetCatalogueId!.Value, sale.SphereLeft!.Value, sale.SphereRight!.Value));
        Assert.Null(sale.LensTypeRefId);
    }

    [Fact]
    public async Task ALeadOnAnOtherLensWhoseTextWasEditedSince_ConvertsWithTheLensesCurrentText()
    {
        // The Lead's lens still matches by power and lens type, so it carries over as a read-only
        // summary — but an admin has since edited the lens's "Other" text. The Sale is recorded
        // from the lens as it is now, not refused on LensTypeOtherText with no field to fix it.
        var otherLensType = Query(db => db.ReferenceDataItems.Single(x => x.Category == ReferenceDataCategory.LensType && x.IsActive && x.IsOtherOption).Id);
        var lensSetId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        factory.Seed(db =>
        {
            db.PresetCatalogues.Add(new PresetCatalogue { Id = lensSetId, Name = $"Other Readers {lensSetId:N}", OwningOrgNodeId = OrganisationSeedConfiguration.DgiId });
            var lensId = Guid.NewGuid();
            db.LensOptions.Add(new LensOption { Id = lensId, PresetCatalogueId = lensSetId, Label = "+1.00 / +2.00 Office", Sphere = 1.00m, Add = 2.00m, LensTypeRefId = otherLensType, LensTypeOtherText = "Office (renamed)" });
            db.LensOptionCoatings.Add(new LensOptionCoating { Id = Guid.NewGuid(), LensOptionId = lensId, CoatingRefId = ReferenceDataSeedConfiguration.CoatingClearId });
            db.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment { Id = Guid.NewGuid(), PresetCatalogueId = lensSetId, OrgNodeId = OrganisationSeedConfiguration.KenyaRetailerId });
            db.Customers.Add(new Customer { Id = customerId, FullName = "Njeri Mwangi", PhoneNumber = "+254744000000", HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath });
            db.Leads.Add(new Lead
            {
                Id = leadId, CustomerId = customerId, TechnicianUserId = Guid.NewGuid(), HierarchyPath = OrganisationSeedConfiguration.KenyaRetailPointPath, ConsentGiven = true,
                LensRangeType = LensRangeType.LensSet, PresetCatalogueId = lensSetId,
                SphereLeft = 1.00m, AddLeft = 2.00m, SphereRight = 1.00m, AddRight = 2.00m,
                LensTypeRefId = otherLensType, LensTypeOtherText = "Office", PresetPupilDistanceBucket = 2,
            });
        });
        var client = factory.CreateAdminClient();

        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");
        await AdminPortalFactory.PostAndFollowAsync(client, $"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token,
            ("Form.ConsentGiven", "true"),
            ("Form.FrameColourRefId", AFrameColour().ToString()),
            ("Form.CoatingRefIds", ReferenceDataSeedConfiguration.CoatingClearId.ToString())));

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == leadId));
        Assert.Equal((otherLensType, "Office (renamed)"), (sale.LensTypeRefId!.Value, sale.LensTypeOtherText));
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

    /// <summary>The value of the option the select tag helper marked selected in the named select,
    /// or null when none is.</summary>
    private static string? SelectedValue(string html, string selectName)
    {
        var select = Regex.Match(html, $"<select[^>]*name=\"{Regex.Escape(selectName)}\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
        Assert.True(select.Success, $"No select named {selectName} on the page.");
        var option = Regex.Match(select.Groups[1].Value, "<option(?=[^>]*selected=\"selected\")[^>]*value=\"([^\"]*)\"");
        return option.Success ? option.Groups[1].Value : null;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Entities;
using DotGlasses.Infrastructure.Persistence;
using DotGlasses.Infrastructure.Persistence.Configurations;
using DotGlasses.Rules.Sales;
using DotGlasses.Web.Tests.DomainRejections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;
using DomainReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;
using FulfilmentStatus = DotGlasses.Domain.Enums.FulfilmentStatus;

namespace DotGlasses.Web.Tests.CustomOrders;

/// <summary>
/// A custom order from end to end over HTTP (ADR-0008): a Lead orders its Custom lens before the
/// customer pays, the queue lists it as "Not yet paid", the Leads list shows how far it has got,
/// and converting that Lead — from the Field App's API or the Admin Portal's form — keeps the lens
/// that was ordered and shares the one order.
///
/// Every test works at its own retail point, so the queue and the Leads list it reads hold only
/// its own rows.
/// </summary>
public class CustomOrderFlowTests(AdminPortalFactory factory) : IClassFixture<AdminPortalFactory>
{
    private static readonly Guid Clear = ReferenceDataSeedConfiguration.CoatingClearId;
    private static readonly Guid BlueBlock = ReferenceDataSeedConfiguration.CoatingBlueBlockId;

    private static int _nextSegment = 500;

    /// <summary>A new retail point under the seeded Kenya retailer, named so a test can find its
    /// own rows on a shared screen.</summary>
    private (string Path, string Name) ARetailPoint()
    {
        var segment = Interlocked.Increment(ref _nextSegment);
        var path = $"{OrganisationSeedConfiguration.KenyaRetailerPath}{segment}/";
        var name = $"Order Flow Outlet {segment}";
        factory.Seed(db => db.OrganisationNodes.Add(new OrganisationNode
        {
            Id = Guid.NewGuid(),
            ParentId = OrganisationSeedConfiguration.KenyaRetailerId,
            Name = name,
            Level = Domain.Enums.OrganisationLevel.RetailPoint,
            HierarchyPath = path,
        }));
        return (path, name);
    }

    private T Query<T>(Func<DotGlassesDbContext, T> query)
    {
        using var scope = factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<DotGlassesDbContext>());
    }

    private Guid ActiveItem(DomainReferenceDataCategory category) =>
        Query(db => db.ReferenceDataItems.First(x => x.Category == category && x.IsActive && !x.IsOtherOption).Id);

    private CreateLeadRequest AnOrderingLead(string fullName = "Amina Okoro") => new()
    {
        Id = Guid.NewGuid(),
        FullName = fullName,
        PhoneNumber = "0700111222",
        Gender = Gender.Female,
        ConsentGiven = true,
        ReasonNotPurchasedRefId = ActiveItem(DomainReferenceDataCategory.ReasonNotPurchased),
        CustomerToldPrice = true,
        LensRangeType = LensRangeType.Custom,
        SphereLeft = -1.25m,
        SphereRight = -2.00m,
        CylinderRight = -0.50m,
        AxisRight = 90m,
        PupilDistanceMm = 62m,
        OrderFromDotGlasses = true,
        CoatingRefIds = [Clear, BlueBlock],
    };

    /// <summary>The Sale a locked conversion form sends: the Lead's lens and Coating set.</summary>
    private CreateSaleRequest ASaleConverting(CreateLeadRequest lead) => new()
    {
        Id = Guid.NewGuid(),
        SourceLeadId = lead.Id,
        FullName = lead.FullName,
        PhoneNumber = lead.PhoneNumber,
        Gender = lead.Gender,
        ConsentGiven = true,
        LensRangeType = LensRangeType.Custom,
        SphereLeft = lead.SphereLeft,
        SphereRight = lead.SphereRight,
        CylinderRight = lead.CylinderRight,
        AxisRight = lead.AxisRight,
        PupilDistanceMm = lead.PupilDistanceMm,
        FrameColourRefId = ActiveItem(DomainReferenceDataCategory.FrameColour),
        FrameCoverage = ContractFrameCoverage.FullFrame,
        CoatingRefIds = [.. lead.CoatingRefIds],
    };

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray());
    }

    private static async Task AssertCreatedAsync(HttpResponseMessage response) =>
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

    private List<CustomOrder> OrdersAt(string path) =>
        Query(db => db.CustomOrders.IgnoreQueryFilters().Where(o => o.HierarchyPath == path).ToList());

    /// <summary>The queue as a DGI admin sees it, entity-decoded.</summary>
    private async Task<string> QueueAsync(string query = "") =>
        WebUtility.HtmlDecode(await factory.CreateAdminClient().GetStringAsync($"/CustomOrders{query}"));

    /// <summary>The slice of the queue for one retail point: from its heading to the next.</summary>
    private static string RetailPointSection(string html, string retailPointName)
    {
        var start = html.IndexOf(retailPointName, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{retailPointName} is not on the Custom Orders screen.");
        var next = html.IndexOf("<h3", start, StringComparison.Ordinal);
        return next < 0 ? html[start..] : html[start..next];
    }

    // --- Ordering from a Lead -----------------------------------------------------------------

    [Fact]
    public async Task ACustomLeadWithTheTick_PlacesAnOrder_AndSaysSo()
    {
        var (path, _) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);
        var request = AnOrderingLead();

        var response = await client.PostAsJsonAsync("api/v1/leads", request);

        await AssertCreatedAsync(response);
        var created = (await response.Content.ReadFromJsonAsync<LeadDto>())!;
        Assert.True(created.OrderFromDotGlasses);
        Assert.Equal(CustomOrderStatus.Submitted, created.CustomOrderStatus);
        Assert.Equal(new[] { Clear, BlueBlock }.Order(), created.CoatingRefIds.Order());

        var order = Assert.Single(OrdersAt(path));
        Assert.Equal(request.Id, order.LeadId);
        Assert.Null(order.SaleId);
        Assert.Equal(FulfilmentStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task TheSameOrderingLeadSentTwice_PlacesOneOrder()
    {
        var (path, _) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);
        var request = AnOrderingLead();

        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/leads", request));
        var again = await client.PostAsJsonAsync("api/v1/leads", request);

        Assert.True(again.IsSuccessStatusCode, await again.Content.ReadAsStringAsync());
        Assert.Single(OrdersAt(path));
    }

    [Fact]
    public async Task ALensSetLeadWithTheTick_IsRefusedAgainstTheTick()
    {
        var (path, _) = ARetailPoint();
        var request = AnOrderingLead();
        request.LensRangeType = null;
        request.SphereLeft = request.SphereRight = request.CylinderRight = request.AxisRight = request.PupilDistanceMm = null;
        request.CoatingRefIds = [];

        var errors = await ErrorsAsync(await factory.CreateTechnicianClient(path).PostAsJsonAsync("api/v1/leads", request));

        Assert.Equal("OrderFromDotGlasses", Assert.Single(errors).Key);
        Assert.Empty(OrdersAt(path));
    }

    [Fact]
    public async Task AnOrderingLeadWithAnIncompleteLensOrNoCoatings_IsRefusedAgainstEachField()
    {
        var (path, _) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);

        var noPupilDistance = AnOrderingLead();
        noPupilDistance.PupilDistanceMm = null;
        Assert.Equal("PupilDistanceMm", Assert.Single(await ErrorsAsync(await client.PostAsJsonAsync("api/v1/leads", noPupilDistance))).Key);

        var oneEye = AnOrderingLead();
        oneEye.SphereLeft = null;
        Assert.Equal("LensRangeType", Assert.Single(await ErrorsAsync(await client.PostAsJsonAsync("api/v1/leads", oneEye))).Key);

        var noCoatings = AnOrderingLead();
        noCoatings.CoatingRefIds = [];
        Assert.Equal("CoatingRefIds", Assert.Single(await ErrorsAsync(await client.PostAsJsonAsync("api/v1/leads", noCoatings))).Key);

        var noLensType = AnOrderingLead();
        noLensType.AddLeft = noLensType.AddRight = 2.00m;
        Assert.Equal("LensTypeRefId", Assert.Single(await ErrorsAsync(await client.PostAsJsonAsync("api/v1/leads", noLensType))).Key);

        Assert.Empty(OrdersAt(path));
        Assert.Empty(Query(db => db.Leads.IgnoreQueryFilters().Where(l => l.HierarchyPath == path).ToList()));
    }

    [Fact]
    public async Task ALeadThatIsNotOrdering_KeepsItsPreferenceAndPlacesNoOrder()
    {
        var (path, _) = ARetailPoint();
        var request = AnOrderingLead();
        request.OrderFromDotGlasses = false;
        request.CoatingRefIds = [];
        request.CoatingPreferenceRefId = BlueBlock;
        request.PupilDistanceMm = null;

        var response = await factory.CreateTechnicianClient(path).PostAsJsonAsync("api/v1/leads", request);

        await AssertCreatedAsync(response);
        var created = (await response.Content.ReadFromJsonAsync<LeadDto>())!;
        Assert.False(created.OrderFromDotGlasses);
        Assert.Null(created.CustomOrderStatus);
        Assert.Equal(BlueBlock, created.CoatingPreferenceRefId);
        Assert.Empty(OrdersAt(path));
    }

    // --- The queue ----------------------------------------------------------------------------

    [Fact]
    public async Task TheQueue_ShowsALeadPlacedOrderAsNotYetPaid_AndASalePlacedOneWithoutTheBadge()
    {
        var (leadPath, leadOutlet) = ARetailPoint();
        var (salePath, saleOutlet) = ARetailPoint();
        await AssertCreatedAsync(await factory.CreateTechnicianClient(leadPath).PostAsJsonAsync("api/v1/leads", AnOrderingLead("Brian Mwangi")));

        var sale = ASaleConverting(AnOrderingLead("Asha Otieno"));
        sale.SourceLeadId = null;
        sale.OrderFromDotGlasses = true;
        await AssertCreatedAsync(await factory.CreateTechnicianClient(salePath).PostAsJsonAsync("api/v1/sales", sale));

        var html = await QueueAsync();

        var leadPlaced = RetailPointSection(html, leadOutlet);
        Assert.Contains("Brian Mwangi", leadPlaced);
        Assert.Contains("Not yet paid", leadPlaced);
        Assert.Contains("-1.25", leadPlaced);

        var salePlaced = RetailPointSection(html, saleOutlet);
        Assert.Contains("Asha Otieno", salePlaced);
        Assert.DoesNotContain("Not yet paid", salePlaced);
    }

    [Fact]
    public async Task AdvancingAnOrderNobodyHasPaidFor_Works_AndTheLeadsListShowsTheNewStatus()
    {
        var (path, _) = ARetailPoint();
        var technician = factory.CreateTechnicianClient(path);
        var request = AnOrderingLead();
        await AssertCreatedAsync(await technician.PostAsJsonAsync("api/v1/leads", request));

        var before = Assert.Single((await technician.GetFromJsonAsync<List<LeadDto>>("api/v1/leads/open"))!);
        Assert.Equal(CustomOrderStatus.Submitted, before.CustomOrderStatus);

        var admin = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(admin, "/CustomOrders");
        var orderId = Assert.Single(OrdersAt(path)).Id;
        var advance = await admin.PostAsync("/CustomOrders/AdvanceStatus", AdminPortalFactory.Form(token, ("orderId", orderId.ToString())));

        Assert.Equal(HttpStatusCode.Redirect, advance.StatusCode);
        Assert.Equal(FulfilmentStatus.InLab, Assert.Single(OrdersAt(path)).Status);

        var after = Assert.Single((await technician.GetFromJsonAsync<List<LeadDto>>("api/v1/leads/open"))!);
        Assert.Equal(CustomOrderStatus.InLab, after.CustomOrderStatus);
        Assert.Equal(CustomOrderStatus.InLab, (await technician.GetFromJsonAsync<LeadDto>($"api/v1/leads/{request.Id}"))!.CustomOrderStatus);
    }

    [Fact]
    public async Task ALeadWithNoOrder_HasNoStatusInTheLeadsList()
    {
        var (path, _) = ARetailPoint();
        var technician = factory.CreateTechnicianClient(path);
        var request = AnOrderingLead();
        request.OrderFromDotGlasses = false;
        request.CoatingRefIds = [];
        await AssertCreatedAsync(await technician.PostAsJsonAsync("api/v1/leads", request));

        var lead = Assert.Single((await technician.GetFromJsonAsync<List<LeadDto>>("api/v1/leads/open"))!);

        Assert.Null(lead.CustomOrderStatus);
        Assert.False(lead.OrderFromDotGlasses);
    }

    [Fact]
    public async Task TheExport_SaysWhetherEachOrderIsPaid()
    {
        var (path, outlet) = ARetailPoint();
        await AssertCreatedAsync(await factory.CreateTechnicianClient(path).PostAsJsonAsync("api/v1/leads", AnOrderingLead("Chege Kariuki")));

        var csv = await factory.CreateAdminClient().GetStringAsync("/CustomOrders/Export");
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Contains("Paid", lines[0].Split(','));
        var row = Assert.Single(lines, l => l.Contains(outlet, StringComparison.Ordinal));
        Assert.Contains("Chege Kariuki", row);
        Assert.Contains(",No,", row);
    }

    // --- Converting an ordered Lead: the Field App's API --------------------------------------

    [Fact]
    public async Task ConvertingAnOrderedLead_LinksTheSaleToTheSameOrder_AndTheQueueShowsItPaid()
    {
        var (path, outlet) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);
        var lead = AnOrderingLead("Dorcas Njoroge");
        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/leads", lead));

        var response = await client.PostAsJsonAsync("api/v1/sales", ASaleConverting(lead));

        await AssertCreatedAsync(response);
        var sale = (await response.Content.ReadFromJsonAsync<SaleDto>())!;
        Assert.True(sale.OrderFromDotGlasses);
        Assert.Equal(CustomOrderStatus.Submitted, sale.CustomOrderStatus);

        var order = Assert.Single(OrdersAt(path));
        Assert.Equal(lead.Id, order.LeadId);
        Assert.Equal(sale.Id, order.SaleId);

        var section = RetailPointSection(await QueueAsync(), outlet);
        Assert.Contains("Dorcas Njoroge", section);
        Assert.DoesNotContain("Not yet paid", section);
        Assert.Contains("1 active", section);
    }

    [Fact]
    public async Task TheSameConversionSentTwice_IsAnsweredNotRefused()
    {
        // The outbox retries a conversion whose first answer never arrived. Refusing it as
        // "already converted" would park a Sale that saved on the Failed records screen.
        var (path, _) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);
        var lead = AnOrderingLead();
        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/leads", lead));
        var sale = ASaleConverting(lead);
        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/sales", sale));

        var again = await client.PostAsJsonAsync("api/v1/sales", sale);

        Assert.True(again.IsSuccessStatusCode, await again.Content.ReadAsStringAsync());
        Assert.Equal(sale.Id, (await again.Content.ReadFromJsonAsync<SaleDto>())!.Id);
        Assert.Single(OrdersAt(path));
        Assert.Single(Query(db => db.Sales.IgnoreQueryFilters().Where(s => s.HierarchyPath == path).ToList()));

        // A different Sale for the same Lead is still refused, against the field.
        var another = ASaleConverting(lead);
        Assert.Contains("SourceLeadId", (await ErrorsAsync(await client.PostAsJsonAsync("api/v1/sales", another))).Keys);
    }

    [Fact]
    public async Task AnOrderedLead_CanStillBeConverted_AfterOneOfItsCoatingsIsRetired()
    {
        // Between ordering and paying, an admin retires a coating. The lens is being made with it
        // and the form shows it read-only, so the Sale must still save — from the Field App and
        // from the Admin Portal.
        var retiring = Guid.NewGuid();
        factory.Seed(db => db.ReferenceDataItems.Add(new ReferenceDataItem
        {
            Id = retiring,
            Category = DomainReferenceDataCategory.Coating,
            Code = $"retiring_{retiring:N}",
            Label = $"Retiring {retiring:N}"[..20],
            IsActive = true,
            SortOrder = 99,
        }));

        var (path, _) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);
        var viaApi = AnOrderingLead("Esther Wambui");
        var viaPortal = AnOrderingLead("Faith Achieng");
        viaApi.CoatingRefIds = viaPortal.CoatingRefIds = [Clear, retiring];
        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/leads", viaApi));
        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/leads", viaPortal));

        factory.Seed(db => db.ReferenceDataItems.Single(x => x.Id == retiring).IsActive = false);

        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/sales", ASaleConverting(viaApi)));
        Assert.Equal(HttpStatusCode.Redirect, (await ConvertOnThePortalAsync(viaPortal.Id)).StatusCode);

        Assert.All(OrdersAt(path), order => Assert.NotNull(order.SaleId));

        // The same retired coating on a Sale that isn't converting an ordered Lead is refused as ever.
        var fresh = ASaleConverting(viaApi);
        fresh.SourceLeadId = null;
        Assert.Contains("CoatingRefIds", (await ErrorsAsync(await client.PostAsJsonAsync("api/v1/sales", fresh))).Keys);
    }

    [Fact]
    public async Task ConvertingAnOrderedLeadWithAChangedLens_IsRefusedAgainstTheFieldThatChanged()
    {
        var (path, _) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);
        var lead = AnOrderingLead();
        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/leads", lead));

        var sale = ASaleConverting(lead);
        sale.SphereLeft = -3.00m;
        sale.PupilDistanceMm = 64m;

        var errors = await ErrorsAsync(await client.PostAsJsonAsync("api/v1/sales", sale));

        Assert.Equal(new[] { "PupilDistanceMm", "SphereLeft" }, errors.Keys.Order());
        Assert.All(errors.Values, messages => Assert.Equal([OrderedLeadConversion.LensLockedMessage], messages));
        AssertNothingWasConverted(path, lead.Id);
    }

    [Fact]
    public async Task ConvertingAnOrderedLeadWithAChangedCoatingSet_IsRefusedAgainstTheCoatings()
    {
        var (path, _) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);
        var lead = AnOrderingLead();
        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/leads", lead));

        var sale = ASaleConverting(lead);
        sale.CoatingRefIds = [Clear];

        var errors = await ErrorsAsync(await client.PostAsJsonAsync("api/v1/sales", sale));

        Assert.Equal([OrderedLeadConversion.CoatingsLockedMessage], Assert.Single(errors, e => e.Key == "CoatingRefIds").Value);
        Assert.Single(errors);
        AssertNothingWasConverted(path, lead.Id);
    }

    [Fact]
    public async Task ConvertingAnOrderedLeadWhileAskingForASecondOrder_IsRefusedAgainstTheTick()
    {
        var (path, _) = ARetailPoint();
        var client = factory.CreateTechnicianClient(path);
        var lead = AnOrderingLead();
        await AssertCreatedAsync(await client.PostAsJsonAsync("api/v1/leads", lead));

        var sale = ASaleConverting(lead);
        sale.OrderFromDotGlasses = true;

        var errors = await ErrorsAsync(await client.PostAsJsonAsync("api/v1/sales", sale));

        Assert.Equal([OrderedLeadConversion.AlreadyOrderedMessage], Assert.Single(errors, e => e.Key == "OrderFromDotGlasses").Value);
        Assert.Single(errors);
        AssertNothingWasConverted(path, lead.Id);
    }

    private void AssertNothingWasConverted(string path, Guid leadId)
    {
        var order = Assert.Single(OrdersAt(path));
        Assert.Null(order.SaleId);
        Assert.Empty(Query(db => db.Sales.IgnoreQueryFilters().Where(s => s.HierarchyPath == path).ToList()));
        Assert.False(Query(db => db.Leads.IgnoreQueryFilters().Single(l => l.Id == leadId)).ConvertedFlag);
    }

    // --- Converting an ordered Lead: the Admin Portal's form ----------------------------------

    private async Task<HttpResponseMessage> ConvertOnThePortalAsync(Guid leadId, params (string Key, string Value)[] fields)
    {
        var client = factory.CreateAdminClient();
        var token = await AdminPortalFactory.GetAntiforgeryTokenAsync(client, $"/Leads/Convert/{leadId}");
        var all = new List<(string Key, string Value)>
        {
            ("Form.ConsentGiven", "true"),
            ("Form.FrameColourRefId", ActiveItem(DomainReferenceDataCategory.FrameColour).ToString()),
        };
        all.AddRange(fields);
        return await client.PostAsync($"/Leads/Convert/{leadId}", AdminPortalFactory.Form(token, all.ToArray()));
    }

    [Fact]
    public async Task ThePortalsConversionForm_ShowsAnOrderedLensReadOnly_WithItsStatus()
    {
        var (path, _) = ARetailPoint();
        var lead = AnOrderingLead();
        await AssertCreatedAsync(await factory.CreateTechnicianClient(path).PostAsJsonAsync("api/v1/leads", lead));

        var html = WebUtility.HtmlDecode(await factory.CreateAdminClient().GetStringAsync($"/Leads/Convert/{lead.Id}"));

        Assert.Contains("id=\"orderedLens\"", html);
        Assert.Contains("This lens is already ordered", html);
        Assert.Contains("Submitted", html);
        Assert.Contains("Coatings ordered:", html);

        // Nothing to choose: no coating boxes and no order tick.
        Assert.DoesNotContain("name=\"Form.CoatingRefIds\"", html);
        Assert.DoesNotContain("name=\"Form.OrderFromDotGlasses\"", html);
    }

    [Fact]
    public async Task ThePortalsConversionForm_ForALeadThatDidNotOrder_StillOffersCoatingsAndTheTick()
    {
        var (path, _) = ARetailPoint();
        var lead = AnOrderingLead();
        lead.OrderFromDotGlasses = false;
        lead.CoatingRefIds = [];
        await AssertCreatedAsync(await factory.CreateTechnicianClient(path).PostAsJsonAsync("api/v1/leads", lead));

        var html = await factory.CreateAdminClient().GetStringAsync($"/Leads/Convert/{lead.Id}");

        Assert.DoesNotContain("id=\"orderedLens\"", html);
        Assert.Contains("name=\"Form.CoatingRefIds\"", html);
        Assert.Contains("name=\"Form.OrderFromDotGlasses\"", html);
    }

    [Fact]
    public async Task ConvertingAnOrderedLeadOnThePortal_KeepsWhatWasOrdered_WhateverIsPosted()
    {
        var (path, _) = ARetailPoint();
        var lead = AnOrderingLead();
        await AssertCreatedAsync(await factory.CreateTechnicianClient(path).PostAsJsonAsync("api/v1/leads", lead));

        // A tampered post: a different coating and a second order request. Neither control is on
        // the form, and neither is taken from it.
        var response = await ConvertOnThePortalAsync(lead.Id,
            ("Form.CoatingRefIds", ReferenceDataSeedConfiguration.CoatingPhotochromicId.ToString()),
            ("Form.OrderFromDotGlasses", "true"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var sale = Query(db => db.Sales.IgnoreQueryFilters().Single(s => s.SourceLeadId == lead.Id));
        Assert.Equal((-1.25m, -2.00m, 62m), (sale.SphereLeft!.Value, sale.SphereRight!.Value, sale.PupilDistanceMm!.Value));
        Assert.Equal(
            new[] { Clear, BlueBlock }.Order(),
            Query(db => db.SaleCoatings.Where(c => c.SaleId == sale.Id).Select(c => c.CoatingRefId).ToList()).Order());

        var order = Assert.Single(OrdersAt(path));
        Assert.Equal(lead.Id, order.LeadId);
        Assert.Equal(sale.Id, order.SaleId);
    }
}

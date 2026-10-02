using DotGlasses.Application.Sales;
using DotGlasses.Application.Tests.Fakes;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Sales;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using ContractFrameCoverage = DotGlasses.Contracts.Sales.FrameCoverage;

namespace DotGlasses.Application.Tests.Sales;

/// <summary>
/// Characterisation tests: these pin the behaviour of recording a Sale — including converting a
/// Lead into one — as it is today, so the rules refactor has a net under it.
/// </summary>
public class SaleServiceTests
{
    private const string RetailPoint = "/1/4/12/";
    private const string AnotherRetailPoint = "/1/4/13/";

    private static readonly Guid BlueBlock = Guid.NewGuid();
    private static readonly Guid Photochromic = Guid.NewGuid();

    private static SaleService CreateSut(
        out FakeSaleRepository sales,
        out FakeLeadRepository leads,
        out FakeCustomerRepository customers,
        out FakeUnitOfWork unitOfWork) =>
        CreateSut(out sales, out leads, out customers, out unitOfWork, out _);

    private static SaleService CreateSut(
        out FakeSaleRepository sales,
        out FakeLeadRepository leads,
        out FakeCustomerRepository customers,
        out FakeUnitOfWork unitOfWork,
        out FakeCustomOrderRepository orders)
    {
        sales = new FakeSaleRepository();
        leads = new FakeLeadRepository();
        customers = new FakeCustomerRepository();
        unitOfWork = new FakeUnitOfWork();
        orders = new FakeCustomOrderRepository();
        return new SaleService(sales, leads, customers, orders, unitOfWork);
    }

    private static CreateSaleRequest ARecordedSale(
        Guid? id = null,
        Guid? sourceLeadId = null,
        string fullName = "Amina Okoro",
        string? phoneNumber = "0700111222",
        LensRangeType lensRangeType = LensRangeType.LensSet,
        bool orderFromDotGlasses = false,
        List<Guid>? coatingRefIds = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            SourceLeadId = sourceLeadId,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            AgeYears = 42,
            Gender = Gender.Female,
            ConsentGiven = true,
            ReferredOrTreated = false,
            LensRangeType = lensRangeType,
            OrderFromDotGlasses = orderFromDotGlasses,
            FrameColourRefId = Guid.NewGuid(),
            FrameCoverage = ContractFrameCoverage.FullFrame,
            CoatingRefIds = coatingRefIds ?? [BlueBlock],
        };

    private static Lead AnOpenLead(Guid id) => new()
    {
        Id = id,
        HierarchyPath = RetailPoint,
        TechnicianUserId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        ConvertedFlag = false,
    };

    [Fact]
    public async Task ACallerWithNoOrgAssignment_IsRefusedAndWritesNothing()
    {
        var sut = CreateSut(out var sales, out _, out var customers, out var unitOfWork);

        var rejection = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.CreateAsync(ARecordedSale(), Guid.NewGuid(), hierarchyPath: ""));

        Assert.Contains("isn't assigned to an organisation", rejection.Message);
        Assert.Equal(0, sales.Count);
        Assert.Equal(0, customers.Count);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConvertingALeadToASale_LinksBothRecordsAndMarksTheLeadConverted()
    {
        var sut = CreateSut(out _, out var leads, out _, out _);
        var leadId = Guid.NewGuid();
        leads.Seed(AnOpenLead(leadId));

        var sale = await sut.CreateAsync(ARecordedSale(sourceLeadId: leadId), Guid.NewGuid(), RetailPoint);

        var sourceLead = leads.Inspect(leadId)!;
        Assert.Equal(leadId, sale.SourceLeadId);
        Assert.Equal(sale.Id, sourceLead.SaleId);
        Assert.True(sourceLead.ConvertedFlag);
    }

    [Fact]
    public async Task ConvertingALeadToASale_CommitsTheSaleAndTheBackLinkTogether()
    {
        // One SaveChangesAsync call is what makes the pair atomic — a Sale must never exist with
        // its source Lead still sitting in the open worklist. See IUnitOfWork.
        var sut = CreateSut(out _, out var leads, out _, out var unitOfWork);
        var leadId = Guid.NewGuid();
        leads.Seed(AnOpenLead(leadId));

        await sut.CreateAsync(ARecordedSale(sourceLeadId: leadId), Guid.NewGuid(), RetailPoint);

        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ASaleRecordedWithoutASourceLead_LeavesEveryOpenLeadOpen()
    {
        var sut = CreateSut(out _, out var leads, out _, out _);
        var unrelatedLeadId = Guid.NewGuid();
        leads.Seed(AnOpenLead(unrelatedLeadId));

        var sale = await sut.CreateAsync(ARecordedSale(), Guid.NewGuid(), RetailPoint);

        Assert.Null(sale.SourceLeadId);
        Assert.False(leads.Inspect(unrelatedLeadId)!.ConvertedFlag);
        Assert.Null(leads.Inspect(unrelatedLeadId)!.SaleId);
    }

    [Fact]
    public async Task ConvertingALeadTheCallerCannotSee_IsRefusedAndWritesNothing()
    {
        // The source Lead is outside the caller's hierarchy scope, so the repository returns
        // nothing. That is a refusal, not a back-link to skip: half-completing would record a
        // Sale while its source Lead stayed in the open worklist, with the caller told it
        // worked. Nothing at all is written — no Sale, no coatings, no Customer, no commit.
        var sut = CreateSut(out var sales, out var leads, out var customers, out var unitOfWork);
        var leadId = Guid.NewGuid();
        leads.Seed(AnOpenLead(leadId));
        leads.HideFromCaller(leadId);

        var rejection = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.CreateAsync(ARecordedSale(sourceLeadId: leadId), Guid.NewGuid(), RetailPoint));

        var sourceLead = leads.Inspect(leadId)!;
        Assert.Contains("isn't available at your location", rejection.Message);
        Assert.Equal(0, sales.Count);
        Assert.Empty(sales.StoredCoatings);
        Assert.Equal(0, customers.Count);
        Assert.False(sourceLead.ConvertedFlag);
        Assert.Null(sourceLead.SaleId);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConvertingALeadThatDoesNotExistAtAll_IsRefusedTheSameWay()
    {
        // "Out of scope" and "never existed" are the same miss through the hierarchy filter, and
        // both refuse — a Sale must never claim a source it could not read.
        var sut = CreateSut(out var sales, out _, out _, out var unitOfWork);

        await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.CreateAsync(ARecordedSale(sourceLeadId: Guid.NewGuid()), Guid.NewGuid(), RetailPoint));

        Assert.Equal(0, sales.Count);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReplayingASaleCreate_ReturnsTheOriginalRecordAndDoesNotDuplicateIt()
    {
        var sut = CreateSut(out var sales, out _, out var customers, out var unitOfWork);
        var id = Guid.NewGuid();

        var original = await sut.CreateAsync(
            ARecordedSale(id, coatingRefIds: [BlueBlock]), Guid.NewGuid(), RetailPoint);
        var replayed = await sut.CreateAsync(
            ARecordedSale(id, fullName: "Someone Else", coatingRefIds: [Photochromic]), Guid.NewGuid(), RetailPoint);

        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal([BlueBlock], replayed.CoatingRefIds);
        Assert.Equal(1, sales.Count);
        Assert.Single(sales.StoredCoatings);
        Assert.Equal(1, customers.Count);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ASaleRoutedForFulfilment_PlacesAnOrderAtTheFirstFulfilmentStatus()
    {
        var sut = CreateSut(out _, out _, out _, out _, out var orders);

        var sale = await sut.CreateAsync(
            ARecordedSale(lensRangeType: LensRangeType.Custom, orderFromDotGlasses: true),
            Guid.NewGuid(),
            RetailPoint);

        Assert.True(sale.OrderFromDotGlasses);
        Assert.Equal(CustomOrderStatus.Submitted, sale.CustomOrderStatus);

        var order = Assert.Single(orders.All);
        Assert.Equal(sale.Id, order.SaleId);
        Assert.Null(order.LeadId);
        Assert.Equal(RetailPoint, order.HierarchyPath);
        Assert.Equal(Domain.Enums.FulfilmentStatus.Submitted, order.Status);
    }

    [Fact]
    public async Task AnOrderingSaleSentTwice_PlacesOneOrder()
    {
        var sut = CreateSut(out _, out _, out _, out _, out var orders);
        var request = ARecordedSale(lensRangeType: LensRangeType.Custom, orderFromDotGlasses: true);

        await sut.CreateAsync(request, Guid.NewGuid(), RetailPoint);
        var again = await sut.CreateAsync(request, Guid.NewGuid(), RetailPoint);

        Assert.Single(orders.All);
        Assert.Equal(CustomOrderStatus.Submitted, again.CustomOrderStatus);
    }

    /// <summary>A Lead that ordered its lens, with the order it placed — the lens block and
    /// Coating set a converting Sale has to keep.</summary>
    private static Lead AnOrderedLead(FakeLeadRepository leads, FakeCustomOrderRepository orders)
    {
        var lead = AnOpenLead(Guid.NewGuid());
        lead.LensRangeType = Domain.Enums.LensRangeType.Custom;
        lead.SphereLeft = -1.25m;
        lead.SphereRight = -1.50m;
        lead.PupilDistanceMm = 62;
        leads.Seed(lead);
        leads.AddCoatings([new LeadCoating { Id = Guid.NewGuid(), LeadId = lead.Id, CoatingRefId = BlueBlock }]);
        orders.Seed(new CustomOrder
        {
            Id = Guid.NewGuid(),
            HierarchyPath = RetailPoint,
            LeadId = lead.Id,
            Status = Domain.Enums.FulfilmentStatus.InLab,
        });
        return lead;
    }

    private static CreateSaleRequest ASaleKeepingTheOrderedLens(Lead lead)
    {
        var request = ARecordedSale(sourceLeadId: lead.Id, lensRangeType: LensRangeType.Custom, coatingRefIds: [BlueBlock]);
        request.SphereLeft = lead.SphereLeft;
        request.SphereRight = lead.SphereRight;
        request.PupilDistanceMm = lead.PupilDistanceMm;
        return request;
    }

    [Fact]
    public async Task ConvertingAnOrderedLead_LinksTheSaleToThatOrderRatherThanPlacingAnother()
    {
        var sut = CreateSut(out _, out var leads, out _, out _, out var orders);
        var lead = AnOrderedLead(leads, orders);

        var sale = await sut.CreateAsync(ASaleKeepingTheOrderedLens(lead), Guid.NewGuid(), RetailPoint);

        var order = Assert.Single(orders.All);
        Assert.Equal(lead.Id, order.LeadId);
        Assert.Equal(sale.Id, order.SaleId);

        // The order keeps the progress it had made before anyone paid.
        Assert.Equal(Domain.Enums.FulfilmentStatus.InLab, order.Status);
        Assert.True(sale.OrderFromDotGlasses);
        Assert.Equal(CustomOrderStatus.InLab, sale.CustomOrderStatus);
    }

    [Fact]
    public async Task ConvertingAnOrderedLeadWithADifferentLens_IsRefusedAndWritesNothing()
    {
        var sut = CreateSut(out var sales, out var leads, out var customers, out var unitOfWork, out var orders);
        var lead = AnOrderedLead(leads, orders);
        var request = ASaleKeepingTheOrderedLens(lead);
        request.SphereLeft = -2.00m;

        var rejection = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.CreateAsync(request, Guid.NewGuid(), RetailPoint));

        Assert.Contains("already ordered", rejection.Message);
        Assert.Equal(0, sales.Count);
        Assert.Equal(0, customers.Count);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Null(orders.All[0].SaleId);
        Assert.False(leads.Inspect(lead.Id)!.ConvertedFlag);
    }

    [Fact]
    public async Task ASecondSaleConvertingTheSameLead_IsRefused_AndTheFirstKeepsTheLeadAndItsOrder()
    {
        var sut = CreateSut(out var sales, out var leads, out _, out _, out var orders);
        var lead = AnOrderedLead(leads, orders);
        var first = await sut.CreateAsync(ASaleKeepingTheOrderedLens(lead), Guid.NewGuid(), RetailPoint);

        var rejection = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.CreateAsync(ASaleKeepingTheOrderedLens(lead), Guid.NewGuid(), RetailPoint));

        Assert.Contains("already been converted", rejection.Message);
        Assert.Equal(1, sales.Count);
        Assert.Equal(first.Id, leads.Inspect(lead.Id)!.SaleId);
        Assert.Equal(first.Id, Assert.Single(orders.All).SaleId);
    }

    [Fact]
    public async Task TheSameConversionSentTwice_IsAnsweredWithTheSaleThatExists()
    {
        var sut = CreateSut(out var sales, out var leads, out _, out var unitOfWork, out var orders);
        var lead = AnOrderedLead(leads, orders);
        var request = ASaleKeepingTheOrderedLens(lead);

        var first = await sut.CreateAsync(request, Guid.NewGuid(), RetailPoint);
        var again = await sut.CreateAsync(request, Guid.NewGuid(), RetailPoint);

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, sales.Count);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(orders.All);
    }

    [Fact]
    public async Task ConvertingAnOrderedLeadWhileAskingForASecondOrder_IsRefused()
    {
        var sut = CreateSut(out _, out var leads, out _, out _, out var orders);
        var lead = AnOrderedLead(leads, orders);
        var request = ASaleKeepingTheOrderedLens(lead);
        request.OrderFromDotGlasses = true;

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => sut.CreateAsync(request, Guid.NewGuid(), RetailPoint));

        Assert.Single(orders.All);
    }

    [Fact]
    public async Task ASaleNotRoutedForFulfilment_PlacesNoOrderAtAll()
    {
        // Nothing to advance through the lab/pickup queue — the glasses were handed over from
        // stock on the spot, so the Custom Orders screen must never see this row.
        var sut = CreateSut(out _, out _, out _, out _, out var orders);

        var sale = await sut.CreateAsync(
            ARecordedSale(lensRangeType: LensRangeType.Custom, orderFromDotGlasses: false),
            Guid.NewGuid(),
            RetailPoint);

        Assert.False(sale.OrderFromDotGlasses);
        Assert.Null(sale.CustomOrderStatus);
        Assert.Empty(orders.All);
    }

    [Fact]
    public async Task APresetRangeSale_PlacesNoOrder()
    {
        var sut = CreateSut(out _, out _, out _, out _, out var orders);

        await sut.CreateAsync(
            ARecordedSale(lensRangeType: LensRangeType.LensSet), Guid.NewGuid(), RetailPoint);

        Assert.Empty(orders.All);
    }

    [Fact]
    public async Task ACoatingNamedTwiceOnOneSale_IsRecordedOnceInTheCoatingSet()
    {
        // The Coating set is a set: a duplicate entry — from a coating pairing auto-adding one the
        // technician had already picked, say — must not become two rows on the lens.
        var sut = CreateSut(out var sales, out _, out _, out _);

        var sale = await sut.CreateAsync(
            ARecordedSale(coatingRefIds: [BlueBlock, Photochromic, BlueBlock]), Guid.NewGuid(), RetailPoint);

        var storedForThisSale = sales.StoredCoatings.Where(c => c.SaleId == sale.Id).ToList();
        Assert.Equal(2, storedForThisSale.Count);
        Assert.Equal([BlueBlock, Photochromic], storedForThisSale.Select(c => c.CoatingRefId).ToList());
    }

    [Fact]
    public async Task TheCoatingSetReadBackFromARecordedSale_ContainsEachCoatingOnce()
    {
        var sut = CreateSut(out _, out _, out _, out _);

        var sale = await sut.CreateAsync(
            ARecordedSale(coatingRefIds: [BlueBlock, Photochromic, BlueBlock]), Guid.NewGuid(), RetailPoint);

        // Today the DTO handed straight back from the create echoes the request's own list,
        // duplicates and all, while every later read returns the de-duplicated stored set. Pinned
        // as-is: this is the current behaviour, not the intended one.
        Assert.Equal([BlueBlock, Photochromic, BlueBlock], sale.CoatingRefIds);

        var readBack = await sut.GetByIdAsync(sale.Id);
        Assert.Equal([BlueBlock, Photochromic], readBack!.CoatingRefIds);
    }

    [Fact]
    public async Task AFirstTimeCustomer_IsCreatedAtTheCallersRetailPoint()
    {
        var sut = CreateSut(out _, out _, out var customers, out _);

        var sale = await sut.CreateAsync(ARecordedSale(), Guid.NewGuid(), RetailPoint);

        var customer = Assert.Single(customers.All);
        Assert.Equal(sale.CustomerId, customer.Id);
        Assert.Equal(RetailPoint, customer.HierarchyPath);
        Assert.Equal("Amina Okoro", customer.FullName);
        Assert.Equal("0700111222", customer.PhoneNumber);
    }

    [Fact]
    public async Task ARepeatCustomerAtTheSameRetailPoint_IsMatchedRatherThanDuplicated()
    {
        var sut = CreateSut(out _, out _, out var customers, out _);

        var first = await sut.CreateAsync(ARecordedSale(), Guid.NewGuid(), RetailPoint);
        var second = await sut.CreateAsync(ARecordedSale(), Guid.NewGuid(), RetailPoint);

        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal(1, customers.Count);
    }

    [Fact]
    public async Task TheSameNameAndPhoneAtADifferentRetailPoint_IsADifferentCustomer()
    {
        var sut = CreateSut(out _, out _, out var customers, out _);

        var here = await sut.CreateAsync(ARecordedSale(), Guid.NewGuid(), RetailPoint);
        var elsewhere = await sut.CreateAsync(ARecordedSale(), Guid.NewGuid(), AnotherRetailPoint);

        Assert.NotEqual(here.CustomerId, elsewhere.CustomerId);
        Assert.Equal(2, customers.Count);
    }

    [Fact]
    public async Task TheSameNameWithADifferentPhoneNumber_IsADifferentCustomer()
    {
        var sut = CreateSut(out _, out _, out var customers, out _);

        var first = await sut.CreateAsync(ARecordedSale(phoneNumber: "0700111222"), Guid.NewGuid(), RetailPoint);
        var second = await sut.CreateAsync(ARecordedSale(phoneNumber: "0700999888"), Guid.NewGuid(), RetailPoint);

        Assert.NotEqual(first.CustomerId, second.CustomerId);
        Assert.Equal(2, customers.Count);
    }

    [Fact]
    public async Task ACustomerRecordedWithNoPhoneNumber_IsMatchedOnNameAloneNextTime()
    {
        // A Sale may carry no phone number at all. Two nameless-phone records for the same name
        // at the same retail point are the same person, not two.
        var sut = CreateSut(out _, out _, out var customers, out _);

        var first = await sut.CreateAsync(ARecordedSale(phoneNumber: null), Guid.NewGuid(), RetailPoint);
        var second = await sut.CreateAsync(ARecordedSale(phoneNumber: null), Guid.NewGuid(), RetailPoint);

        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal(1, customers.Count);
    }

    [Fact]
    public async Task ARecordedSale_IsAttributedToTheCallersRetailPointAndTechnician()
    {
        var sut = CreateSut(out _, out _, out _, out _);
        var technician = Guid.NewGuid();

        var sale = await sut.CreateAsync(ARecordedSale(), technician, RetailPoint);

        Assert.Equal(RetailPoint, sale.HierarchyPath);
        Assert.Equal(technician, sale.TechnicianUserId);
    }

    [Fact]
    public async Task ARecordedSale_IsReadableBackWithItsCoatingSet()
    {
        var sut = CreateSut(out _, out _, out _, out _);

        var sale = await sut.CreateAsync(
            ARecordedSale(coatingRefIds: [BlueBlock, Photochromic]), Guid.NewGuid(), RetailPoint);
        var readBack = await sut.GetByIdAsync(sale.Id);

        Assert.NotNull(readBack);
        Assert.Equal([BlueBlock, Photochromic], readBack!.CoatingRefIds);
    }
}

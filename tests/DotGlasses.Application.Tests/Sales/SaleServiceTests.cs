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
        out FakeUnitOfWork unitOfWork)
    {
        sales = new FakeSaleRepository();
        leads = new FakeLeadRepository();
        customers = new FakeCustomerRepository();
        unitOfWork = new FakeUnitOfWork();
        return new SaleService(sales, leads, customers, unitOfWork);
    }

    private static CreateSaleRequest ARecordedSale(
        Guid? id = null,
        Guid? sourceLeadId = null,
        string fullName = "Amina Okoro",
        string? phoneNumber = "0700111222",
        LensRangeType lensRangeType = LensRangeType.SixLensSet,
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
    public async Task ASaleRoutedForFulfilment_StartsAtTheFirstFulfilmentStatus()
    {
        var sut = CreateSut(out var sales, out _, out _, out _);

        var sale = await sut.CreateAsync(
            ARecordedSale(lensRangeType: LensRangeType.Custom, orderFromDotGlasses: true),
            Guid.NewGuid(),
            RetailPoint);

        Assert.True(sale.OrderFromDotGlasses);
        Assert.Equal(Domain.Enums.FulfilmentStatus.Submitted, sales.Inspect(sale.Id)!.FulfilmentStatus);
    }

    [Fact]
    public async Task ASaleNotRoutedForFulfilment_HasNoFulfilmentStatusAtAll()
    {
        // Nothing to advance through the lab/pickup queue — the glasses were handed over from
        // stock on the spot, so the Custom Orders screen must never see this row.
        var sut = CreateSut(out var sales, out _, out _, out _);

        var sale = await sut.CreateAsync(
            ARecordedSale(lensRangeType: LensRangeType.Custom, orderFromDotGlasses: false),
            Guid.NewGuid(),
            RetailPoint);

        Assert.False(sale.OrderFromDotGlasses);
        Assert.Null(sales.Inspect(sale.Id)!.FulfilmentStatus);
    }

    [Fact]
    public async Task APresetRangeSale_HasNoFulfilmentStatus()
    {
        var sut = CreateSut(out var sales, out _, out _, out _);

        var sale = await sut.CreateAsync(
            ARecordedSale(lensRangeType: LensRangeType.SixLensSet), Guid.NewGuid(), RetailPoint);

        Assert.Null(sales.Inspect(sale.Id)!.FulfilmentStatus);
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

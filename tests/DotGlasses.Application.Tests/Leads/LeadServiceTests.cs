using DotGlasses.Application.Leads;
using DotGlasses.Application.Tests.Fakes;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Leads;
using DotGlasses.Domain.Common;

namespace DotGlasses.Application.Tests.Leads;

/// <summary>
/// Characterisation tests: these pin the behaviour of recording a Lead — including converting a
/// Test into one — as it is today, so the rules refactor has a net under it.
/// </summary>
public class LeadServiceTests
{
    private const string RetailPoint = "/1/4/12/";
    private const string AnotherRetailPoint = "/1/4/13/";

    private static LeadService CreateSut(
        out FakeLeadRepository leads,
        out FakeVisionTestRepository tests,
        out FakeCustomerRepository customers,
        out FakeUnitOfWork unitOfWork) =>
        CreateSut(out leads, out tests, out customers, out unitOfWork, out _);

    private static LeadService CreateSut(
        out FakeLeadRepository leads,
        out FakeVisionTestRepository tests,
        out FakeCustomerRepository customers,
        out FakeUnitOfWork unitOfWork,
        out FakeCustomOrderRepository orders)
    {
        leads = new FakeLeadRepository();
        tests = new FakeVisionTestRepository();
        customers = new FakeCustomerRepository();
        unitOfWork = new FakeUnitOfWork();
        orders = new FakeCustomOrderRepository();
        return new LeadService(leads, tests, customers, orders, unitOfWork);
    }

    private static readonly Guid BlueBlock = Guid.NewGuid();
    private static readonly Guid Photochromic = Guid.NewGuid();

    /// <summary>A Custom-range Lead that orders its lens, with the Coating set it orders.</summary>
    private static CreateLeadRequest AnOrderingLead(Guid? id = null)
    {
        var request = ARecordedLead(id);
        request.LensRangeType = LensRangeType.Custom;
        request.OrderFromDotGlasses = true;
        request.CoatingRefIds = [BlueBlock, Photochromic, BlueBlock];
        return request;
    }

    [Fact]
    public async Task ALeadThatOrdersItsLens_PlacesOneOrderAndKeepsItsCoatingSet()
    {
        var sut = CreateSut(out var leads, out _, out _, out var unitOfWork, out var orders);

        var lead = await sut.CreateAsync(AnOrderingLead(), Guid.NewGuid(), RetailPoint);

        var order = Assert.Single(orders.All);
        Assert.Equal(lead.Id, order.LeadId);
        Assert.Null(order.SaleId);
        Assert.Equal(RetailPoint, order.HierarchyPath);
        Assert.Equal(Domain.Enums.FulfilmentStatus.Submitted, order.Status);

        // The Coating set is a set: a coating named twice is stored once.
        Assert.Equal([BlueBlock, Photochromic], leads.StoredCoatings.Select(c => c.CoatingRefId).ToList());
        Assert.Equal([BlueBlock, Photochromic], lead.CoatingRefIds);
        Assert.True(lead.OrderFromDotGlasses);
        Assert.Equal(CustomOrderStatus.Submitted, lead.CustomOrderStatus);

        // The Lead and its order are one unit of work.
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ALeadThatDoesNotOrder_PlacesNoOrderAndStoresNoCoatingSet()
    {
        var sut = CreateSut(out var leads, out _, out _, out _, out var orders);

        var lead = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);

        Assert.Empty(orders.All);
        Assert.Empty(leads.StoredCoatings);
        Assert.False(lead.OrderFromDotGlasses);
        Assert.Null(lead.CustomOrderStatus);
    }

    [Fact]
    public async Task AnOrderingLeadSentTwice_PlacesOneOrder()
    {
        // The outbox retries a record whose first response never arrived.
        var sut = CreateSut(out var leads, out _, out _, out var unitOfWork, out var orders);
        var request = AnOrderingLead();

        var first = await sut.CreateAsync(request, Guid.NewGuid(), RetailPoint);
        var again = await sut.CreateAsync(request, Guid.NewGuid(), RetailPoint);

        Assert.Equal(first.Id, again.Id);
        Assert.Single(orders.All);
        Assert.Equal(1, leads.Count);
        Assert.Equal(2, leads.StoredCoatings.Count);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(CustomOrderStatus.Submitted, again.CustomOrderStatus);
    }

    [Fact]
    public async Task AnOrderedLeadReadBack_ShowsItsOrdersCurrentStatus()
    {
        var sut = CreateSut(out _, out _, out _, out _, out var orders);
        var lead = await sut.CreateAsync(AnOrderingLead(), Guid.NewGuid(), RetailPoint);

        orders.All[0].Status = Domain.Enums.FulfilmentStatus.ReadyForPickup;

        var open = Assert.Single(await sut.ListOpenAsync());
        Assert.Equal(lead.Id, open.Id);
        Assert.Equal(CustomOrderStatus.ReadyForPickup, open.CustomOrderStatus);
    }

    private static CreateLeadRequest ARecordedLead(
        Guid? id = null,
        Guid? sourceTestId = null,
        string fullName = "Amina Okoro",
        string phoneNumber = "0700111222") => new()
        {
            Id = id ?? Guid.NewGuid(),
            SourceTestId = sourceTestId,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            AgeYears = 42,
            Gender = Gender.Female,
            ConsentGiven = true,
            ReferredOrTreated = false,
            ReasonNotPurchasedRefId = Guid.NewGuid(),
            LensRangeType = LensRangeType.LensSet,
        };

    private static Domain.Entities.Test AnExistingTest(Guid id) => new()
    {
        Id = id,
        HierarchyPath = RetailPoint,
        TechnicianUserId = Guid.NewGuid(),
    };

    [Fact]
    public async Task ACallerWithNoOrgAssignment_IsRefusedAndWritesNothing()
    {
        var sut = CreateSut(out _, out _, out var customers, out var unitOfWork);

        var rejection = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), hierarchyPath: ""));

        Assert.Contains("isn't assigned to an organisation", rejection.Message);
        Assert.Empty(await sut.ListAsync());
        Assert.Equal(0, customers.Count);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task LookingUpAnOpenLead_WithNoOrgAssignment_IsRefused()
    {
        var sut = CreateSut(out _, out _, out _, out _);

        var rejection = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.FindOpenMatchAsync(hierarchyPath: "", "Amina Okoro", "0700111222"));

        Assert.Contains("isn't assigned to an organisation", rejection.Message);
    }

    [Fact]
    public async Task ConvertingATestToALead_LinksBothRecordsInBothDirections()
    {
        var sut = CreateSut(out _, out var tests, out _, out _);
        var testId = Guid.NewGuid();
        var sourceTest = AnExistingTest(testId);
        tests.Seed(sourceTest);

        var lead = await sut.CreateAsync(ARecordedLead(sourceTestId: testId), Guid.NewGuid(), RetailPoint);

        Assert.Equal(testId, lead.SourceTestId);
        Assert.Equal(lead.Id, tests.Inspect(testId)!.ConvertedToLeadId);
    }

    [Fact]
    public async Task ConvertingATestToALead_CommitsTheLeadAndTheBackLinkTogether()
    {
        // One SaveChangesAsync call is what makes the pair atomic — a Lead must never exist with
        // its source Test still showing as unconverted. See IUnitOfWork.
        var sut = CreateSut(out _, out var tests, out _, out var unitOfWork);
        var testId = Guid.NewGuid();
        tests.Seed(AnExistingTest(testId));

        await sut.CreateAsync(ARecordedLead(sourceTestId: testId), Guid.NewGuid(), RetailPoint);

        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ALeadRecordedWithoutASourceTest_LinksToNothing()
    {
        var sut = CreateSut(out _, out var tests, out _, out _);
        var unrelatedTestId = Guid.NewGuid();
        tests.Seed(AnExistingTest(unrelatedTestId));

        var lead = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);

        Assert.Null(lead.SourceTestId);
        Assert.Null(tests.Inspect(unrelatedTestId)!.ConvertedToLeadId);
    }

    [Fact]
    public async Task ConvertingATestTheCallerCannotSee_IsRefusedAndWritesNothing()
    {
        // The source Test is outside the caller's hierarchy scope, so the repository returns
        // nothing. That is a refusal, not a back-link to skip: half-completing would leave a Lead
        // recorded against a Test that still reads as unconverted, with the caller told it
        // worked. Nothing at all is written — no Lead, no Customer, no commit.
        var sut = CreateSut(out _, out var tests, out var customers, out var unitOfWork);
        var testId = Guid.NewGuid();
        tests.Seed(AnExistingTest(testId));
        tests.HideFromCaller(testId);

        var rejection = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.CreateAsync(ARecordedLead(sourceTestId: testId), Guid.NewGuid(), RetailPoint));

        Assert.Contains("isn't available at your location", rejection.Message);
        Assert.Empty(await sut.ListAsync());
        Assert.Equal(0, customers.Count);
        Assert.Null(tests.Inspect(testId)!.ConvertedToLeadId);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConvertingATestThatDoesNotExistAtAll_IsRefusedTheSameWay()
    {
        // "Out of scope" and "never existed" are the same miss through the hierarchy filter, and
        // both refuse — a Lead must never claim a source it could not read.
        var sut = CreateSut(out _, out _, out _, out var unitOfWork);

        await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => sut.CreateAsync(ARecordedLead(sourceTestId: Guid.NewGuid()), Guid.NewGuid(), RetailPoint));

        Assert.Empty(await sut.ListAsync());
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReplayingALeadCreate_ReturnsTheOriginalRecordAndDoesNotDuplicateIt()
    {
        var sut = CreateSut(out _, out _, out var customers, out var unitOfWork);
        var id = Guid.NewGuid();

        var original = await sut.CreateAsync(ARecordedLead(id, fullName: "Amina Okoro"), Guid.NewGuid(), RetailPoint);
        var replayed = await sut.CreateAsync(ARecordedLead(id, fullName: "Someone Else"), Guid.NewGuid(), RetailPoint);

        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal("Amina Okoro", replayed.CustomerFullName);
        Assert.Single(await sut.ListAsync());
        Assert.Equal(1, customers.Count);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AFirstTimeCustomer_IsCreatedAtTheCallersRetailPoint()
    {
        var sut = CreateSut(out _, out _, out var customers, out _);

        var lead = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);

        var customer = Assert.Single(customers.All);
        Assert.Equal(lead.CustomerId, customer.Id);
        Assert.Equal(RetailPoint, customer.HierarchyPath);
        Assert.Equal("Amina Okoro", customer.FullName);
        Assert.Equal("0700111222", customer.PhoneNumber);
    }

    [Fact]
    public async Task ARepeatCustomerAtTheSameRetailPoint_IsMatchedRatherThanDuplicated()
    {
        var sut = CreateSut(out _, out _, out var customers, out _);

        var first = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);
        var second = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);

        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal(1, customers.Count);
    }

    [Fact]
    public async Task TheSameNameAndPhoneAtADifferentRetailPoint_IsADifferentCustomer()
    {
        // Matching is scoped to the retail point — two outlets each keep their own Customer row
        // for the same person rather than sharing one.
        var sut = CreateSut(out _, out _, out var customers, out _);

        var here = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);
        var elsewhere = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), AnotherRetailPoint);

        Assert.NotEqual(here.CustomerId, elsewhere.CustomerId);
        Assert.Equal(2, customers.Count);
    }

    [Fact]
    public async Task TheSameNameWithADifferentPhoneNumber_IsADifferentCustomer()
    {
        // The match is exact on both fields, with no fuzzy matching — one field differing is a
        // new Customer, not a near-miss to be reconciled.
        var sut = CreateSut(out _, out _, out var customers, out _);

        var first = await sut.CreateAsync(ARecordedLead(phoneNumber: "0700111222"), Guid.NewGuid(), RetailPoint);
        var second = await sut.CreateAsync(ARecordedLead(phoneNumber: "0700999888"), Guid.NewGuid(), RetailPoint);

        Assert.NotEqual(first.CustomerId, second.CustomerId);
        Assert.Equal(2, customers.Count);
    }

    [Fact]
    public async Task ADifferentNameOnTheSamePhoneNumber_IsADifferentCustomer()
    {
        var sut = CreateSut(out _, out _, out var customers, out _);

        var first = await sut.CreateAsync(ARecordedLead(fullName: "Amina Okoro"), Guid.NewGuid(), RetailPoint);
        var second = await sut.CreateAsync(ARecordedLead(fullName: "Amina Okoro-Bello"), Guid.NewGuid(), RetailPoint);

        Assert.NotEqual(first.CustomerId, second.CustomerId);
        Assert.Equal(2, customers.Count);
    }

    [Fact]
    public async Task LookingForAnOpenLead_FindsTheOneRecordedForThatExactNameAndPhone()
    {
        var sut = CreateSut(out _, out _, out _, out _);
        var recorded = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);

        var match = await sut.FindOpenMatchAsync(RetailPoint, "Amina Okoro", "0700111222");

        Assert.NotNull(match);
        Assert.Equal(recorded.Id, match!.Id);
    }

    [Fact]
    public async Task LookingForAnOpenLead_FindsNothingWhenNoCustomerMatchesExactly()
    {
        var sut = CreateSut(out _, out _, out _, out _);
        await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);

        Assert.Null(await sut.FindOpenMatchAsync(RetailPoint, "Amina Okoro", "0700999888"));
        Assert.Null(await sut.FindOpenMatchAsync(AnotherRetailPoint, "Amina Okoro", "0700111222"));
    }

    [Fact]
    public async Task AnAlreadyConvertedLead_IsNotOfferedAsAnOpenMatch()
    {
        var sut = CreateSut(out var leads, out _, out _, out _);
        var recorded = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);
        var stored = leads.Inspect(recorded.Id)!;
        stored.ConvertedFlag = true;
        stored.SaleId = Guid.NewGuid();

        Assert.Null(await sut.FindOpenMatchAsync(RetailPoint, "Amina Okoro", "0700111222"));
        Assert.Empty(await sut.ListOpenAsync());
    }

    [Fact]
    public async Task ALeadCarriesTheCustomersNameAndPhoneForDisplay()
    {
        var sut = CreateSut(out _, out _, out _, out _);

        var lead = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);
        var readBack = await sut.GetByIdAsync(lead.Id);

        Assert.Equal("Amina Okoro", readBack!.CustomerFullName);
        Assert.Equal("0700111222", readBack.CustomerPhoneNumber);
    }

    [Fact]
    public async Task ARecordedLead_IsOpenAndUnsoldUntilItIsConverted()
    {
        var sut = CreateSut(out _, out _, out _, out _);

        var lead = await sut.CreateAsync(ARecordedLead(), Guid.NewGuid(), RetailPoint);

        Assert.False(lead.ConvertedFlag);
        Assert.Null(lead.SaleId);
        Assert.Single(await sut.ListOpenAsync());
    }

    [Fact]
    public async Task ARecordedLead_IsAttributedToTheCallersRetailPointAndTechnician()
    {
        var sut = CreateSut(out _, out _, out _, out _);
        var technician = Guid.NewGuid();

        var lead = await sut.CreateAsync(ARecordedLead(), technician, RetailPoint);

        Assert.Equal(RetailPoint, lead.HierarchyPath);
        Assert.Equal(technician, lead.TechnicianUserId);
    }
}

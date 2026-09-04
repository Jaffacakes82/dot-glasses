using DotGlasses.Application.Tests.Fakes;
using DotGlasses.Application.VisionTests;
using DotGlasses.Contracts.Common;
using DotGlasses.Contracts.Tests;

namespace DotGlasses.Application.Tests.VisionTests;

/// <summary>
/// Characterisation tests: these pin the behaviour of recording a Test as it is today, so the
/// rules refactor has a net under it. They assert what the service guarantees at its interface,
/// not how it produces it.
/// </summary>
public class VisionTestServiceTests
{
    private const string RetailPoint = "/1/4/12/";

    private static VisionTestService CreateSut(
        out FakeVisionTestRepository tests,
        out FakeUnitOfWork unitOfWork)
    {
        tests = new FakeVisionTestRepository();
        unitOfWork = new FakeUnitOfWork();
        return new VisionTestService(tests, unitOfWork);
    }

    private static CreateTestRequest ARecordedTest(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        AgeYears = 42,
        Gender = Gender.Female,
        Outcome = TestOutcome.NeedsGlasses,
        ReferredOrTreated = false,
        LensRangeType = LensRangeType.SixLensSet,
    };

    [Fact]
    public async Task ARecordedTest_IsAttributedToTheCallersRetailPointAndTechnician()
    {
        var sut = CreateSut(out _, out _);
        var technician = Guid.NewGuid();

        var recorded = await sut.CreateAsync(ARecordedTest(), technician, RetailPoint);

        // The request DTO deliberately carries neither of these — they are stamped from the
        // authenticated caller, never accepted from the body. See CLAUDE.md's offline-sync note.
        Assert.Equal(RetailPoint, recorded.HierarchyPath);
        Assert.Equal(technician, recorded.TechnicianUserId);
    }

    [Fact]
    public async Task ARecordedTest_CarriesNoLeadLinkUntilItIsConverted()
    {
        var sut = CreateSut(out _, out _);

        var recorded = await sut.CreateAsync(ARecordedTest(), Guid.NewGuid(), RetailPoint);

        Assert.Null(recorded.ConvertedToLeadId);
    }

    [Fact]
    public async Task ARecordedTest_IsReadableBackById()
    {
        var sut = CreateSut(out _, out _);
        var recorded = await sut.CreateAsync(ARecordedTest(), Guid.NewGuid(), RetailPoint);

        var readBack = await sut.GetByIdAsync(recorded.Id);

        Assert.NotNull(readBack);
        Assert.Equal(recorded.Id, readBack!.Id);
        Assert.Equal(TestOutcome.NeedsGlasses, readBack.Outcome);
    }

    [Fact]
    public async Task ReplayingATestCreate_ReturnsTheOriginalRecordAndDoesNotDuplicateIt()
    {
        // The offline outbox retries a queued create until it is acknowledged, reusing the same
        // client-generated id. A replay must be a no-op, not a second record and not an overwrite.
        var sut = CreateSut(out _, out var unitOfWork);
        var id = Guid.NewGuid();

        var first = ARecordedTest(id);
        first.AgeYears = 42;
        var original = await sut.CreateAsync(first, Guid.NewGuid(), RetailPoint);

        var replay = ARecordedTest(id);
        replay.AgeYears = 99;
        var replayed = await sut.CreateAsync(replay, Guid.NewGuid(), RetailPoint);

        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal(42, replayed.AgeYears);
        Assert.Single(await sut.ListAsync());
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReplayingATestCreate_KeepsTheOriginalCallersAttribution()
    {
        // Consequence of the replay being a no-op: whoever was signed in when the record first
        // landed stays on it, even if a different technician's device drains the queue.
        var sut = CreateSut(out _, out _);
        var id = Guid.NewGuid();
        var originalTechnician = Guid.NewGuid();

        await sut.CreateAsync(ARecordedTest(id), originalTechnician, RetailPoint);
        var replayed = await sut.CreateAsync(ARecordedTest(id), Guid.NewGuid(), "/1/9/99/");

        Assert.Equal(originalTechnician, replayed.TechnicianUserId);
        Assert.Equal(RetailPoint, replayed.HierarchyPath);
    }
}

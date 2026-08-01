using DotGlasses.Application.WidgetExamples;
using DotGlasses.Contracts.WidgetExamples;

namespace DotGlasses.Application.Tests.WidgetExamples;

public class WidgetExampleServiceTests
{
    private static WidgetExampleService CreateSut(out FakeWidgetExampleRepository repository)
    {
        repository = new FakeWidgetExampleRepository();
        return new WidgetExampleService(repository);
    }

    [Fact]
    public async Task CreateAsync_NewId_AddsAndReturnsIt()
    {
        var sut = CreateSut(out _);
        var request = new CreateWidgetExampleRequest { Id = Guid.NewGuid(), Name = "Test", HierarchyPath = "/1/" };

        var result = await sut.CreateAsync(request);

        Assert.Equal(request.Id, result.Id);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task CreateAsync_RepeatedWithSameId_IsIdempotent_DoesNotOverwrite()
    {
        var sut = CreateSut(out _);
        var id = Guid.NewGuid();

        var first = await sut.CreateAsync(new CreateWidgetExampleRequest { Id = id, Name = "Original", HierarchyPath = "/1/" });

        // Simulates a retried offline-sync replay of the same client-generated id with a
        // (hypothetically) different payload — the original record must win, not the replay.
        var second = await sut.CreateAsync(new CreateWidgetExampleRequest { Id = id, Name = "Replayed", HierarchyPath = "/1/" });

        Assert.Equal("Original", first.Name);
        Assert.Equal("Original", second.Name);

        var listed = await sut.ListAsync();
        Assert.Single(listed);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        var sut = CreateSut(out _);

        var result = await sut.UpdateAsync(Guid.NewGuid(), new UpdateWidgetExampleRequest { Name = "Doesn't matter" });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_KnownId_UpdatesNameAndDescription()
    {
        var sut = CreateSut(out _);
        var created = await sut.CreateAsync(new CreateWidgetExampleRequest { Id = Guid.NewGuid(), Name = "Before", HierarchyPath = "/1/" });

        var updated = await sut.UpdateAsync(created.Id, new UpdateWidgetExampleRequest { Name = "After", Description = "New description" });

        Assert.NotNull(updated);
        Assert.Equal("After", updated!.Name);
        Assert.Equal("New description", updated.Description);
    }

    [Fact]
    public async Task DeleteAsync_KnownId_RemovesIt_ReturnsTrue()
    {
        var sut = CreateSut(out _);
        var created = await sut.CreateAsync(new CreateWidgetExampleRequest { Id = Guid.NewGuid(), Name = "Test", HierarchyPath = "/1/" });

        var deleted = await sut.DeleteAsync(created.Id);
        var afterDelete = await sut.GetByIdAsync(created.Id);

        Assert.True(deleted);
        Assert.Null(afterDelete);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        var sut = CreateSut(out _);

        var deleted = await sut.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }
}

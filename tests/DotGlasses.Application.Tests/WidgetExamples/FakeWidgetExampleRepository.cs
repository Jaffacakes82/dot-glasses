using DotGlasses.Application.WidgetExamples;
using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Tests.WidgetExamples;

public class FakeWidgetExampleRepository : IWidgetExampleRepository
{
    private readonly Dictionary<Guid, WidgetExample> _store = [];

    public Task<WidgetExample?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<WidgetExample>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WidgetExample>>(_store.Values.ToList());

    public Task AddAsync(WidgetExample entity, CancellationToken cancellationToken = default)
    {
        _store[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WidgetExample entity, CancellationToken cancellationToken = default)
    {
        _store[entity.Id] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(WidgetExample entity, CancellationToken cancellationToken = default)
    {
        _store.Remove(entity.Id);
        return Task.CompletedTask;
    }
}

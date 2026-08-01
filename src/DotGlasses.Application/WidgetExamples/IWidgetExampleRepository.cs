using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.WidgetExamples;

public interface IWidgetExampleRepository
{
    Task<WidgetExample?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WidgetExample>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(WidgetExample entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(WidgetExample entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(WidgetExample entity, CancellationToken cancellationToken = default);
}

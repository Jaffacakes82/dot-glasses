using DotGlasses.Contracts.WidgetExamples;

namespace DotGlasses.Application.WidgetExamples;

public interface IWidgetExampleService
{
    Task<WidgetExampleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WidgetExampleDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Idempotent upsert keyed on <see cref="CreateWidgetExampleRequest.Id"/> — a
    /// replayed offline-sync create for a record that already exists is a no-op, returning the
    /// existing record rather than overwriting it.</summary>
    Task<WidgetExampleDto> CreateAsync(CreateWidgetExampleRequest request, CancellationToken cancellationToken = default);

    Task<WidgetExampleDto?> UpdateAsync(Guid id, UpdateWidgetExampleRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

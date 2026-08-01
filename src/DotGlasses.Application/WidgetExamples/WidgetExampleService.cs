using DotGlasses.Contracts.WidgetExamples;
using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.WidgetExamples;

public class WidgetExampleService(IWidgetExampleRepository repository) : IWidgetExampleService
{
    public async Task<WidgetExampleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<WidgetExampleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.ListAsync(cancellationToken);
        return entities.Select(ToDto).ToList();
    }

    public async Task<WidgetExampleDto> CreateAsync(CreateWidgetExampleRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var entity = new WidgetExample
        {
            Id = request.Id,
            Name = request.Name,
            Description = request.Description,
            HierarchyPath = request.HierarchyPath,
        };

        await repository.AddAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<WidgetExampleDto?> UpdateAsync(Guid id, UpdateWidgetExampleRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Name = request.Name;
        entity.Description = request.Description;

        await repository.UpdateAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        await repository.DeleteAsync(entity, cancellationToken);
        return true;
    }

    private static WidgetExampleDto ToDto(WidgetExample entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        HierarchyPath = entity.HierarchyPath,
        CreatedAtUtc = entity.CreatedAtUtc,
        ModifiedAtUtc = entity.ModifiedAtUtc,
    };
}

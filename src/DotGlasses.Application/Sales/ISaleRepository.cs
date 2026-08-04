using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Sales;

/// <summary>Add/Update only track changes (no auto-save) — see IVisionTestRepository.</summary>
public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sale>> ListAsync(CancellationToken cancellationToken = default);
    void Add(Sale entity);
}

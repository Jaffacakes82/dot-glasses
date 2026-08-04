using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Leads;

/// <summary>Add/Update only track changes (no auto-save) — see IVisionTestRepository.</summary>
public interface ILeadRepository
{
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken = default);
    void Add(Lead entity);
    void Update(Lead entity);
}

using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Leads;

/// <summary>Add/Update only track changes (no auto-save) — see IVisionTestRepository.</summary>
public interface ILeadRepository
{
    Task<Lead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>ConvertedFlag == false only.</summary>
    Task<IReadOnlyList<Lead>> ListOpenAsync(CancellationToken cancellationToken = default);

    /// <summary>Most recently created open Lead for customerId, or null.</summary>
    Task<Lead?> FindOpenByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    void Add(Lead entity);
    void Update(Lead entity);
}

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

    /// <summary>The Coating set of each ordering Lead, keyed by Lead id (LeadCoating, ADR-0008) —
    /// batched so a list's DTO mapping doesn't run one query per Lead. A Lead that didn't order
    /// has no rows and is absent.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCoatingRefIdsByLeadIdsAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken = default);

    void AddCoatings(IEnumerable<LeadCoating> coatings);
}

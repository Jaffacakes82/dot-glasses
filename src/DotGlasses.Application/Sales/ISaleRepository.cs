using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Sales;

/// <summary>Add/Update only track changes (no auto-save) — see IVisionTestRepository. Coating is
/// a separate join table (SaleCoating, see ADR-0001) rather than a field on Sale, so it needs its
/// own read/write methods here alongside Sale's own.</summary>
public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sale>> ListAsync(CancellationToken cancellationToken = default);
    void Add(Sale entity);

    /// <summary>Every CoatingRefId for the given Sale ids, keyed by SaleId — batched so ListAsync's
    /// DTO mapping doesn't run one query per Sale.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCoatingRefIdsBySaleIdsAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken cancellationToken = default);

    void AddCoatings(IEnumerable<SaleCoating> coatings);
}

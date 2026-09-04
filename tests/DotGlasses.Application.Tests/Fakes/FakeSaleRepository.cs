using DotGlasses.Application.Sales;
using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Tests.Fakes;

/// <summary>
/// Dictionary-backed stand-in for the EF repository. The Coating set lives in its own join table
/// (SaleCoating, ADR-0001), so it gets its own list here rather than a field on the Sale.
/// </summary>
public class FakeSaleRepository : ISaleRepository
{
    private readonly Dictionary<Guid, Sale> _store = [];
    private readonly List<SaleCoating> _coatings = [];

    public void Seed(Sale entity) => _store[entity.Id] = entity;

    /// <summary>Reads a row regardless of scope — for assertions only, never for the service.</summary>
    public Sale? Inspect(Guid id) => _store.GetValueOrDefault(id);

    public int Count => _store.Count;

    public IReadOnlyList<SaleCoating> StoredCoatings => _coatings;

    public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<Sale>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Sale>>(_store.Values.ToList());

    public void Add(Sale entity) => _store[entity.Id] = entity;

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCoatingRefIdsBySaleIdsAsync(
        IReadOnlyCollection<Guid> saleIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
            _coatings
                .Where(x => saleIds.Contains(x.SaleId))
                .GroupBy(x => x.SaleId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.CoatingRefId).ToList()));

    public void AddCoatings(IEnumerable<SaleCoating> coatings) => _coatings.AddRange(coatings);
}

using DotGlasses.Application.CustomOrders;
using DotGlasses.Domain.Entities;

namespace DotGlasses.Application.Tests.Fakes;

/// <summary>
/// List-backed stand-in for the EF repository over the custom order record (ADR-0008). An order
/// is found by the Lead that placed it or by the Sale it is linked to, as the real one is.
/// </summary>
public class FakeCustomOrderRepository : ICustomOrderRepository
{
    private readonly List<CustomOrder> _store = [];

    public void Seed(CustomOrder entity) => _store.Add(entity);

    public IReadOnlyList<CustomOrder> All => _store;

    public Task<IReadOnlyDictionary<Guid, CustomOrder>> GetByLeadIdsAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CustomOrder>>(
            _store.Where(o => o.LeadId is { } id && leadIds.Contains(id)).ToDictionary(o => o.LeadId!.Value));

    public Task<IReadOnlyDictionary<Guid, CustomOrder>> GetBySaleIdsAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CustomOrder>>(
            _store.Where(o => o.SaleId is { } id && saleIds.Contains(id)).ToDictionary(o => o.SaleId!.Value));

    public void Add(CustomOrder entity) => _store.Add(entity);

    public void Update(CustomOrder entity)
    {
    }
}

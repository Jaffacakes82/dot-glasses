using DotGlasses.Application.CustomOrders;
using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class CustomOrderRepository(DotGlassesDbContext dbContext) : ICustomOrderRepository
{
    public async Task<IReadOnlyDictionary<Guid, CustomOrder>> GetByLeadIdsAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken = default) =>
        await dbContext.CustomOrders
            .Where(x => x.LeadId != null && leadIds.Contains(x.LeadId.Value))
            .ToDictionaryAsync(x => x.LeadId!.Value, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, CustomOrder>> GetBySaleIdsAsync(IReadOnlyCollection<Guid> saleIds, CancellationToken cancellationToken = default) =>
        await dbContext.CustomOrders
            .Where(x => x.SaleId != null && saleIds.Contains(x.SaleId.Value))
            .ToDictionaryAsync(x => x.SaleId!.Value, cancellationToken);

    public void Add(CustomOrder entity) => dbContext.CustomOrders.Add(entity);

    public void Update(CustomOrder entity) => dbContext.CustomOrders.Update(entity);
}

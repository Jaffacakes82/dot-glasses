using DotGlasses.Application.Sales;
using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class SaleRepository(DotGlassesDbContext dbContext) : ISaleRepository
{
    public async Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Sales.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Sale>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Sales.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public void Add(Sale entity) => dbContext.Sales.Add(entity);
}

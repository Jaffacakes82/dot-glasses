using DotGlasses.Application.Leads;
using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class LeadRepository(DotGlassesDbContext dbContext) : ILeadRepository
{
    public async Task<Lead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Leads.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public void Add(Lead entity) => dbContext.Leads.Add(entity);

    public void Update(Lead entity) => dbContext.Leads.Update(entity);
}

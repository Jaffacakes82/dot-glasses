using DotGlasses.Application.WidgetExamples;
using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class WidgetExampleRepository(DotGlassesDbContext dbContext) : IWidgetExampleRepository
{
    public async Task<WidgetExample?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.WidgetExamples.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WidgetExample>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.WidgetExamples.OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task AddAsync(WidgetExample entity, CancellationToken cancellationToken = default)
    {
        dbContext.WidgetExamples.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WidgetExample entity, CancellationToken cancellationToken = default)
    {
        dbContext.WidgetExamples.Update(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(WidgetExample entity, CancellationToken cancellationToken = default)
    {
        dbContext.WidgetExamples.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

using DotGlasses.Application.VisionTests;
using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class TestRepository(DotGlassesDbContext dbContext) : IVisionTestRepository
{
    public async Task<Test?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Tests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Test>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Tests.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public void Add(Test entity) => dbContext.Tests.Add(entity);

    public void Update(Test entity) => dbContext.Tests.Update(entity);
}

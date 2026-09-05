using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class ReferenceDataLookupService(DotGlassesDbContext dbContext) : IReferenceDataLookupService
{
    public async Task<ReferenceDataLookupResult?> LookupAsync(Guid id, ReferenceDataCategory category, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.ReferenceDataItems
            .Where(x => x.Id == id && x.Category == category)
            .Select(x => new { x.IsActive, x.IsOtherOption })
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? null : new ReferenceDataLookupResult(item.IsActive, item.IsOtherOption);
    }
}

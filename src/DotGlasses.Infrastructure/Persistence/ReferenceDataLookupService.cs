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

    public async Task<bool> LensOptionBelongsToCatalogueAsync(Guid lensOptionId, Guid presetCatalogueId, CancellationToken cancellationToken = default) =>
        await dbContext.LensOptions.AnyAsync(x => x.Id == lensOptionId && x.PresetCatalogueId == presetCatalogueId, cancellationToken);
}

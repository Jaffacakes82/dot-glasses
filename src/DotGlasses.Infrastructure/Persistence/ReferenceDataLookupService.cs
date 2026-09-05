using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Entities;
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

    public async Task<bool> IsCoatingAvailableForLensOptionAsync(Guid lensOptionId, Guid coatingRefId, CancellationToken cancellationToken = default)
    {
        var lensStrengthRefId = await dbContext.LensOptions
            .Where(x => x.Id == lensOptionId)
            .Select(x => (Guid?)x.LensStrengthRefId)
            .FirstOrDefaultAsync(cancellationToken);

        if (lensStrengthRefId is null)
        {
            return false;
        }

        return await dbContext.LensStrengthCoatingOptions
            .AnyAsync(x => x.LensStrengthRefId == lensStrengthRefId && x.CoatingRefId == coatingRefId, cancellationToken);
    }

    public async Task<bool> AreCoatingsExcludedAsync(Guid coatingRefIdA, Guid coatingRefIdB, CancellationToken cancellationToken = default)
    {
        var (lower, higher) = CoatingExclusion.Canonicalize(coatingRefIdA, coatingRefIdB);
        return await dbContext.CoatingExclusions.AnyAsync(x => x.CoatingRefIdA == lower && x.CoatingRefIdB == higher, cancellationToken);
    }
}

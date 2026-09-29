using DotGlasses.Application.Common;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class ReferenceDataQueryService(DotGlassesDbContext dbContext) : IReferenceDataQueryService
{
    public async Task<IReadOnlyList<ReferenceDataItemDto>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ReferenceDataItems
            .Where(x => x.IsActive)
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        return items.Select(x => new ReferenceDataItemDto
        {
            Id = x.Id,
            Category = x.Category.ToContract(),
            Code = x.Code,
            Label = x.Label,
            SortOrder = x.SortOrder,
            IsOtherOption = x.IsOtherOption,
            ImageUrl = x.ImageUrl,
        }).ToList();
    }

    public async Task<CoatingRulesDto> GetCoatingRulesAsync(CancellationToken cancellationToken = default)
    {
        var exclusions = await dbContext.CoatingExclusions.ToListAsync(cancellationToken);

        return new CoatingRulesDto
        {
            Exclusions = exclusions.Select(e => new CoatingExclusionDto { Id = e.Id, CoatingRefIdA = e.CoatingRefIdA, CoatingRefIdB = e.CoatingRefIdB }).ToList(),
        };
    }
}

using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.ReferenceData;
using Microsoft.EntityFrameworkCore;
using DomainCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;
using ContractCategory = DotGlasses.Contracts.Common.ReferenceDataCategory;

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
            Category = ToContractCategory(x.Category),
            Code = x.Code,
            Label = x.Label,
            SortOrder = x.SortOrder,
            IsOtherOption = x.IsOtherOption,
            ImageUrl = x.ImageUrl,
        }).ToList();
    }

    private static ContractCategory ToContractCategory(DomainCategory category) => category switch
    {
        DomainCategory.Occupation => ContractCategory.Occupation,
        DomainCategory.ReasonNotPurchased => ContractCategory.ReasonNotPurchased,
        DomainCategory.ReferralReason => ContractCategory.ReferralReason,
        DomainCategory.Coating => ContractCategory.Coating,
        DomainCategory.FrameColour => ContractCategory.FrameColour,
        DomainCategory.HardCaseColour => ContractCategory.HardCaseColour,
        DomainCategory.LensStrength => ContractCategory.LensStrength,
        DomainCategory.LensType => ContractCategory.LensType,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}

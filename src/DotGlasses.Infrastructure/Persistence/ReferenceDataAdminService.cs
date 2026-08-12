using System.Text;
using System.Text.RegularExpressions;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — no
/// repository interface exists for ReferenceDataItem, and one isn't needed for four
/// straightforward operations (matches how PresetCatalogueQueryService queries DbContext
/// directly).</summary>
public partial class ReferenceDataAdminService(DotGlassesDbContext dbContext) : IReferenceDataAdminService
{
    public async Task<IReadOnlyList<ReferenceDataAdminItem>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.ReferenceDataItems
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        return items.Select(ToAdminItem).ToList();
    }

    public async Task<bool> HasActiveOtherOptionAsync(ReferenceDataCategory category, CancellationToken cancellationToken = default) =>
        await dbContext.ReferenceDataItems
            .AnyAsync(x => x.Category == category && x.IsActive && x.IsOtherOption, cancellationToken);

    public async Task<ReferenceDataAdminItem> CreateAsync(ReferenceDataCategory category, string label, string? imageUrl, bool isOtherOption, CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await dbContext.ReferenceDataItems
            .Where(x => x.Category == category)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var entity = new ReferenceDataItem
        {
            Id = Guid.NewGuid(),
            Category = category,
            Code = Slugify(label),
            Label = label,
            SortOrder = maxSortOrder + 1,
            IsActive = true,
            IsOtherOption = isOtherOption,
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
        };

        dbContext.ReferenceDataItems.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToAdminItem(entity);
    }

    public async Task<ReferenceDataAdminItem> UpdateAsync(Guid id, string label, string? imageUrl, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        entity.Label = label;
        entity.ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToAdminItem(entity);
    }

    public async Task MoveUpAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        var neighbor = await dbContext.ReferenceDataItems
            .Where(x => x.Category == entity.Category && x.IsActive && x.SortOrder < entity.SortOrder)
            .OrderByDescending(x => x.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (neighbor is null)
        {
            return;
        }

        (entity.SortOrder, neighbor.SortOrder) = (neighbor.SortOrder, entity.SortOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveDownAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        var neighbor = await dbContext.ReferenceDataItems
            .Where(x => x.Category == entity.Category && x.IsActive && x.SortOrder > entity.SortOrder)
            .OrderBy(x => x.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (neighbor is null)
        {
            return;
        }

        (entity.SortOrder, neighbor.SortOrder) = (neighbor.SortOrder, entity.SortOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        entity.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ReferenceDataItems.FirstAsync(x => x.Id == id, cancellationToken);
        entity.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ReferenceDataAdminItem ToAdminItem(ReferenceDataItem entity) =>
        new(entity.Id, entity.Category, entity.Code, entity.Label, entity.SortOrder, entity.IsActive, entity.IsOtherOption, entity.ImageUrl);

    private static string Slugify(string label)
    {
        var lowered = label.Trim().ToLowerInvariant();
        var withHyphens = NonAlphanumericRun().Replace(lowered, "-");
        return withHyphens.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRun();
}

using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — no repository
/// interface exists for PresetCatalogue/LensOption/LensStrengthCoatingOption, matching
/// ReferenceDataAdminService/OrganisationAdminService.</summary>
public class PresetCatalogueAdminService(DotGlassesDbContext dbContext) : IPresetCatalogueAdminService
{
    public async Task<IReadOnlyList<PresetCatalogueAdminDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var catalogues = await dbContext.PresetCatalogues.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var catalogueIds = catalogues.Select(c => c.Id).ToList();

        var lensOptions = await dbContext.LensOptions
            .Where(l => catalogueIds.Contains(l.PresetCatalogueId))
            .OrderBy(l => l.SortOrder)
            .ToListAsync(cancellationToken);

        var lensStrengthIds = lensOptions.Select(l => l.LensStrengthRefId).Distinct().ToList();
        var lensStrengthLabels = await dbContext.ReferenceDataItems
            .Where(r => lensStrengthIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Label, cancellationToken);

        var assignmentCounts = (await dbContext.PresetCatalogueAssignments
            .Where(a => catalogueIds.Contains(a.PresetCatalogueId))
            .ToListAsync(cancellationToken))
            .GroupBy(a => a.PresetCatalogueId)
            .ToDictionary(g => g.Key, g => g.Count());

        return catalogues.Select(c => new PresetCatalogueAdminDto(
            c.Id,
            c.Name,
            c.Description,
            c.RangeDescription,
            c.OwningOrgNodeId,
            lensOptions.Where(l => l.PresetCatalogueId == c.Id)
                .Select(l => new PresetCatalogueLensOptionAdminDto(l.Id, l.LensStrengthRefId, lensStrengthLabels.GetValueOrDefault(l.LensStrengthRefId, "Unknown"), l.SortOrder))
                .ToList(),
            assignmentCounts.GetValueOrDefault(c.Id, 0)))
            .ToList();
    }

    public async Task<PresetCatalogueAdminDto> CreateAsync(string name, string? description, string? rangeDescription, Guid owningOrgNodeId, CancellationToken cancellationToken = default)
    {
        var owningOrg = await dbContext.OrganisationNodes.FirstAsync(x => x.Id == owningOrgNodeId, cancellationToken);
        if (owningOrg.Level is not (OrganisationLevel.Dgi or OrganisationLevel.Country))
        {
            throw new InvalidOperationException("A PresetCatalogue's owning org must be Dgi or Country level.");
        }

        var entity = new PresetCatalogue
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            RangeDescription = rangeDescription,
            OwningOrgNodeId = owningOrgNodeId,
        };

        dbContext.PresetCatalogues.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PresetCatalogueAdminDto(entity.Id, entity.Name, entity.Description, entity.RangeDescription, entity.OwningOrgNodeId, [], 0);
    }

    public async Task UpdateAsync(Guid id, string name, string? description, string? rangeDescription, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PresetCatalogues.FirstAsync(x => x.Id == id, cancellationToken);
        entity.Name = name;
        entity.Description = description;
        entity.RangeDescription = rangeDescription;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PresetCatalogueLensOptionAdminDto> AddLensOptionAsync(Guid catalogueId, Guid lensStrengthRefId, CancellationToken cancellationToken = default)
    {
        var maxSortOrder = await dbContext.LensOptions
            .Where(l => l.PresetCatalogueId == catalogueId)
            .Select(l => (int?)l.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var entity = new LensOption
        {
            Id = Guid.NewGuid(),
            PresetCatalogueId = catalogueId,
            LensStrengthRefId = lensStrengthRefId,
            SortOrder = maxSortOrder + 1,
        };

        dbContext.LensOptions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var label = await dbContext.ReferenceDataItems
            .Where(r => r.Id == lensStrengthRefId)
            .Select(r => r.Label)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        return new PresetCatalogueLensOptionAdminDto(entity.Id, entity.LensStrengthRefId, label, entity.SortOrder);
    }

    public async Task RemoveLensOptionAsync(Guid lensOptionId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LensOptions.FirstAsync(x => x.Id == lensOptionId, cancellationToken);
        dbContext.LensOptions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignCatalogueToOrgAsync(Guid catalogueId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var alreadyAssigned = await dbContext.PresetCatalogueAssignments
            .AnyAsync(a => a.PresetCatalogueId == catalogueId && a.OrgNodeId == orgNodeId, cancellationToken);
        if (alreadyAssigned)
        {
            return;
        }

        dbContext.PresetCatalogueAssignments.Add(new PresetCatalogueAssignment
        {
            Id = Guid.NewGuid(),
            PresetCatalogueId = catalogueId,
            OrgNodeId = orgNodeId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListAvailableCoatingsAsync(Guid lensStrengthRefId, CancellationToken cancellationToken = default) =>
        await dbContext.LensStrengthCoatingOptions
            .Where(x => x.LensStrengthRefId == lensStrengthRefId)
            .Select(x => x.CoatingRefId)
            .ToListAsync(cancellationToken);

    public async Task AddAvailableCoatingAsync(Guid lensStrengthRefId, Guid coatingRefId, CancellationToken cancellationToken = default)
    {
        var alreadyAvailable = await dbContext.LensStrengthCoatingOptions
            .AnyAsync(x => x.LensStrengthRefId == lensStrengthRefId && x.CoatingRefId == coatingRefId, cancellationToken);
        if (alreadyAvailable)
        {
            return;
        }

        dbContext.LensStrengthCoatingOptions.Add(new LensStrengthCoatingOption
        {
            Id = Guid.NewGuid(),
            LensStrengthRefId = lensStrengthRefId,
            CoatingRefId = coatingRefId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAvailableCoatingAsync(Guid lensStrengthRefId, Guid coatingRefId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LensStrengthCoatingOptions
            .FirstOrDefaultAsync(x => x.LensStrengthRefId == lensStrengthRefId && x.CoatingRefId == coatingRefId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.LensStrengthCoatingOptions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

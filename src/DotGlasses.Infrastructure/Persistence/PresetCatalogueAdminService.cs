using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DotGlasses.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — no repository
/// interface exists for PresetCatalogue/LensOption/LensStrengthCoatingOption, matching
/// ReferenceDataAdminService/OrganisationAdminService.</summary>
public class PresetCatalogueAdminService(DotGlassesDbContext dbContext, IReferenceDataSnapshotProvider referenceDataSnapshotProvider) : IPresetCatalogueAdminService
{
    public async Task<IReadOnlyList<PresetCatalogueAdminDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await ToAdminDtosAsync(await dbContext.PresetCatalogues.OrderBy(x => x.Name).ToListAsync(cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<PresetCatalogueAdminDto>> ListRetiredAsync(CancellationToken cancellationToken = default) =>
        await ToAdminDtosAsync(
            await dbContext.PresetCatalogues.IgnoreQueryFilters().Where(x => x.IsDeleted).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            cancellationToken);

    public async Task RetireAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PresetCatalogues.FirstAsync(x => x.Id == id, cancellationToken);

        // Remove() on an ISoftDeletable entity is turned into a soft-delete by
        // AuditSaveChangesInterceptor — historical Tests/Leads/Sales still name this lens set by
        // PresetCatalogueId, so it must never be hard-deleted. Its assignments are left alone, so
        // reactivating restores it exactly where it was offered before.
        dbContext.PresetCatalogues.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters() — the row being reactivated is exactly the one the soft-delete
        // filter hides. AuditSaveChangesInterceptor has no "undelete", so the fields are cleared
        // by hand (same as OrganisationAdminService.SetActiveAsync).
        var entity = await dbContext.PresetCatalogues.IgnoreQueryFilters().FirstAsync(x => x.Id == id, cancellationToken);
        entity.IsDeleted = false;
        entity.DeletedAtUtc = null;
        entity.DeletedBy = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsNameTakenAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        // The soft-delete filter leaves retired lens sets out, which is the rule: their names are free.
        var normalized = name.Trim().ToLower();
        return await dbContext.PresetCatalogues
            .AnyAsync(c => c.Name.Trim().ToLower() == normalized && c.Id != (excludeId ?? Guid.Empty), cancellationToken);
    }

    private async Task<IReadOnlyList<PresetCatalogueAdminDto>> ToAdminDtosAsync(List<PresetCatalogue> catalogues, CancellationToken cancellationToken)
    {
        var catalogueIds = catalogues.Select(c => c.Id).ToList();

        var lensOptions = await dbContext.LensOptions
            .Where(l => catalogueIds.Contains(l.PresetCatalogueId))
            .OrderBy(l => l.SortOrder)
            .ToListAsync(cancellationToken);

        var referenceData = await referenceDataSnapshotProvider.GetAsync(cancellationToken);

        return catalogues.Select(c => new PresetCatalogueAdminDto(
            c.Id,
            c.Name,
            c.Description,
            c.OwningOrgNodeId,
            lensOptions.Where(l => l.PresetCatalogueId == c.Id)
                .Select(l => new PresetCatalogueLensOptionAdminDto(l.Id, l.LensStrengthRefId, referenceData.ResolveLabel(l.LensStrengthRefId), l.SortOrder))
                .ToList()))
            .ToList();
    }

    public async Task<PresetCatalogueAdminDto> CreateAsync(string name, string? description, Guid owningOrgNodeId, CancellationToken cancellationToken = default)
    {
        var owningOrg = await dbContext.OrganisationNodes.FirstAsync(x => x.Id == owningOrgNodeId, cancellationToken);
        if (owningOrg.Level is not (OrganisationLevel.Dgi or OrganisationLevel.Country))
        {
            throw new DomainRuleViolationException("A PresetCatalogue's owning org must be Dgi or Country level.");
        }

        var entity = new PresetCatalogue
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            OwningOrgNodeId = owningOrgNodeId,
        };

        dbContext.PresetCatalogues.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PresetCatalogueAdminDto(entity.Id, entity.Name, entity.Description, entity.OwningOrgNodeId, []);
    }

    public async Task UpdateAsync(Guid id, string name, string? description, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PresetCatalogues.FirstAsync(x => x.Id == id, cancellationToken);
        entity.Name = name;
        entity.Description = description;
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

        // The snapshot is safe to read after the write above: this method adds a LensOption, and
        // a LensOption's label comes from the ReferenceDataItem it points at, which nothing here
        // touched. See ReferenceDataSnapshotProvider on the per-request memoization.
        var referenceData = await referenceDataSnapshotProvider.GetAsync(cancellationToken);

        return new PresetCatalogueLensOptionAdminDto(entity.Id, entity.LensStrengthRefId, referenceData.ResolveLabel(lensStrengthRefId), entity.SortOrder);
    }

    public async Task<bool> LensOptionExistsAsync(Guid catalogueId, Guid lensStrengthRefId, CancellationToken cancellationToken = default) =>
        await dbContext.LensOptions
            .AnyAsync(l => l.PresetCatalogueId == catalogueId && l.LensStrengthRefId == lensStrengthRefId, cancellationToken);

    public async Task RemoveLensOptionAsync(Guid lensOptionId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LensOptions.FirstAsync(x => x.Id == lensOptionId, cancellationToken);
        dbContext.LensOptions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignCatalogueToOrgAsync(Guid catalogueId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        // The assign form never offers a retired lens set; this answers a hand-built POST.
        if (!await dbContext.PresetCatalogues.AnyAsync(x => x.Id == catalogueId, cancellationToken))
        {
            throw new DomainRuleViolationException("This lens set is retired — reactivate it before assigning it.");
        }

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

    public async Task<IReadOnlyList<PresetCatalogueAssignmentAdminDto>> ListAssignedOrgsAsync(Guid catalogueId, CancellationToken cancellationToken = default)
    {
        var orgIds = await dbContext.PresetCatalogueAssignments
            .Where(a => a.PresetCatalogueId == catalogueId)
            .Select(a => a.OrgNodeId)
            .ToListAsync(cancellationToken);

        var orgNames = await dbContext.OrganisationNodes
            .Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        return orgIds.Select(id => new PresetCatalogueAssignmentAdminDto(id, orgNames.GetValueOrDefault(id, "Unknown"))).ToList();
    }

    public async Task UnassignCatalogueFromOrgAsync(Guid catalogueId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PresetCatalogueAssignments
            .FirstOrDefaultAsync(a => a.PresetCatalogueId == catalogueId && a.OrgNodeId == orgNodeId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        dbContext.PresetCatalogueAssignments.Remove(entity);
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

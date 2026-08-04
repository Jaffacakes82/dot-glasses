using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Contracts.PresetCatalogues;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class PresetCatalogueQueryService(DotGlassesDbContext dbContext) : IPresetCatalogueQueryService
{
    public async Task<IReadOnlyList<PresetCatalogueDto>> ListAvailableForCallerAsync(string callerHierarchyPath, CancellationToken cancellationToken = default)
    {
        // "Which catalogues can this caller use" runs in the opposite direction from the
        // standard IHierarchyScoped filter (a catalogue is assigned above the caller, not below
        // it) — not translatable as a single SQL predicate against a per-row column compared to
        // a constant, so the assignment→org-path join is small (few rows) and resolved in
        // memory. See PresetCatalogue's doc comment.
        var assignments = await dbContext.PresetCatalogueAssignments
            .Join(dbContext.OrganisationNodes, a => a.OrgNodeId, o => o.Id, (a, o) => new { a.PresetCatalogueId, o.HierarchyPath })
            .ToListAsync(cancellationToken);

        var catalogueIds = assignments
            .Where(x => callerHierarchyPath.StartsWith(x.HierarchyPath, StringComparison.Ordinal))
            .Select(x => x.PresetCatalogueId)
            .Distinct()
            .ToList();

        if (catalogueIds.Count == 0)
        {
            return [];
        }

        var catalogues = await dbContext.PresetCatalogues
            .Where(c => catalogueIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        var lensOptions = await dbContext.LensOptions
            .Where(l => catalogueIds.Contains(l.PresetCatalogueId))
            .OrderBy(l => l.SortOrder)
            .ToListAsync(cancellationToken);

        return catalogues.Select(c => new PresetCatalogueDto
        {
            Id = c.Id,
            Name = c.Name,
            LensOptions = lensOptions
                .Where(l => l.PresetCatalogueId == c.Id)
                .Select(l => new LensOptionDto
                {
                    Id = l.Id,
                    SphericalPower = l.SphericalPower,
                    IsBifocal = l.IsBifocal,
                    AddPower = l.AddPower,
                    CoatingId = l.CoatingId,
                    SortOrder = l.SortOrder,
                })
                .ToList(),
        }).ToList();
    }
}

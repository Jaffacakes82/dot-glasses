using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.Reporting;
using DotGlasses.Contracts.PresetCatalogues;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class PresetCatalogueQueryService(DotGlassesDbContext dbContext, IUnscopedReportQueryService unscopedReportQueryService) : IPresetCatalogueQueryService
{
    public async Task<IReadOnlyList<PresetCatalogueDto>> ListAvailableForCallerAsync(string callerHierarchyPath, CancellationToken cancellationToken = default)
    {
        // "Which catalogues can this caller use" runs in the opposite direction from the
        // standard IHierarchyScoped filter (a catalogue is assigned above the caller, not below
        // it) — a plain query against OrganisationNodes here would be silently filtered down to
        // the caller's own subtree by the global filter, excluding the ancestor org the
        // assignment actually points at (found live: a RetailPoint-level caller got zero
        // catalogues back even with a real assignment on their Country ancestor). Org paths
        // therefore come from IUnscopedReportQueryService — the one sanctioned way to look
        // outside the caller's hierarchy scope (see CLAUDE.md) — and the assignment→org-path
        // match is resolved in memory, since it's not translatable as a single SQL predicate
        // against a per-row column compared to a constant either way.
        var assignments = await dbContext.PresetCatalogueAssignments.ToListAsync(cancellationToken);
        var orgPaths = (await unscopedReportQueryService.GetOrganisationNodePathsUnscopedAsync(cancellationToken))
            .ToDictionary(x => x.Id, x => x.HierarchyPath);

        var catalogueIds = assignments
            .Where(a => orgPaths.TryGetValue(a.OrgNodeId, out var orgPath) && callerHierarchyPath.StartsWith(orgPath, StringComparison.Ordinal))
            .Select(a => a.PresetCatalogueId)
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

using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Contracts.PresetCatalogues;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>The catalogue *content* (names, lens rosters, per-strength coating availability) comes
/// from IReferenceDataSnapshotProvider — the same snapshot the shared rules check against — so the
/// payload the Field App caches and the facts the server validates against cannot drift apart.
/// Only the "which catalogues may this caller use" assignment filter is queried here, because that
/// is the one part of the answer that depends on the caller.</summary>
public class PresetCatalogueQueryService(DotGlassesDbContext dbContext, IUnscopedReportQueryService unscopedReportQueryService, IReferenceDataSnapshotProvider referenceDataSnapshotProvider) : IPresetCatalogueQueryService
{
    public async Task<IReadOnlyList<PresetCatalogueDto>> ListAvailableForCallerAsync(string callerHierarchyPath, CancellationToken cancellationToken = default)
    {
        // "Which catalogues can this caller use" runs in the opposite direction from the
        // standard IHierarchyScoped filter (a catalogue is assigned above the caller, not below
        // it) — a plain query against OrganisationNodes here would be silently filtered down to
        // the caller's own subtree by the global filter, excluding the ancestor org the
        // assignment actually points at. Org paths therefore come from
        // IUnscopedReportQueryService — the one sanctioned way to look outside the caller's
        // hierarchy scope (see CLAUDE.md) — and the assignment→org-path match is resolved in
        // memory.
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

        var referenceData = await referenceDataSnapshotProvider.GetAsync(cancellationToken);

        return referenceData.PresetCatalogues
            .Where(c => catalogueIds.Contains(c.Id))
            .Select(c => new PresetCatalogueDto
            {
                Id = c.Id,
                Name = c.Name,
                Kind = c.Kind,
                LensOptions = c.LensOptions.Select(l => new LensOptionDto
                {
                    Id = l.Id,
                    Label = l.Label,
                    SortOrder = l.SortOrder,
                    AvailableCoatingIds = l.AvailableCoatingIds,
                }).ToList(),
            })
            .ToList();
    }
}

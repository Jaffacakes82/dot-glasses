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

        // The Field App renders this list as it arrives (ADR-0005): alphabetical, never a retired
        // lens set (the snapshot keeps those for historical labels), and never one with no lens
        // powers — nothing on it could be sold. Ordered here rather than relying on the provider's
        // database ORDER BY, whose result depends on the server's collation.
        return referenceData.PresetCatalogues
            .Where(c => catalogueIds.Contains(c.Id) && c.IsActive && c.LensOptions.Count > 0)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new PresetCatalogueDto
            {
                Id = c.Id,
                Name = c.Name,
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

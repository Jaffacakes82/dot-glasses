using DotGlasses.Application.Common;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.Reporting;
using DotGlasses.Rules.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>
/// Queries DotGlassesDbContext directly rather than through a repository — matches
/// ReferenceDataQueryService/PresetCatalogueQueryService.
///
/// No IsActive filter anywhere: this is the copy that has to carry retired items, both so a
/// historical record still renders its label and so a rule can tell "retired" (present, inactive
/// — reject, and say why) apart from "never existed". None of the tables read here are
/// hierarchy-scoped — reference data and preset catalogues are a single global library visible to
/// every authenticated user (see CLAUDE.md), so there is no caller to scope by. The one thing it
/// needs from the org tree — the path of each org a lens set is assigned to, so a rule can ask
/// whether a lens set reaches a record's location (ADR-0005) — comes through
/// IUnscopedReportQueryService, because an assignment usually sits *above* the caller.
///
/// Memoized for the lifetime of the scope, i.e. one request. Every Admin Portal action that
/// mutates reference data redirects rather than re-rendering, so nothing reads a snapshot it
/// invalidated earlier in the same request; if a future action ever writes and then renders a list
/// in one request, it needs its own read rather than this.
/// </summary>
public class ReferenceDataSnapshotProvider(DotGlassesDbContext dbContext, IUnscopedReportQueryService unscopedReportQueryService) : IReferenceDataSnapshotProvider
{
    private ReferenceDataSnapshot? _snapshot;

    public async Task<ReferenceDataSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is not null)
        {
            return _snapshot;
        }

        var items = await dbContext.ReferenceDataItems
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        // Retired lens sets included, like retired reference items above: historical records still
        // name them, and the rules ask "present and active". PresetCatalogue isn't hierarchy-scoped,
        // so IgnoreQueryFilters() lifts only the soft-delete filter here.
        var catalogues = await dbContext.PresetCatalogues.IgnoreQueryFilters().OrderBy(c => c.Name).ToListAsync(cancellationToken);
        // An interim order — single vision first, then by add, then by sphere — only so the list is
        // deterministic. Lens-power ticket 04 defines the fixed display order in Rules, and every
        // screen should then take it from there rather than from this ORDER BY.
        var lensOptions = await dbContext.LensOptions
            .OrderBy(l => l.Add ?? 0m).ThenBy(l => l.Sphere).ThenBy(l => l.Label)
            .ToListAsync(cancellationToken);
        // A lens's coatings, and its pairings by trigger then paired coating, in the Coating list's
        // own admin-set order (items above are already in it) — so every screen lists them the
        // way Reference Data does, not in whatever order the rows happened to be written.
        var listPosition = items.Select((item, index) => (item.Id, index)).ToDictionary(x => x.Id, x => x.index);
        int PositionOf(Guid refId) => listPosition.GetValueOrDefault(refId, int.MaxValue);
        var lensCoatings = (await dbContext.LensOptionCoatings.ToListAsync(cancellationToken))
            .OrderBy(c => PositionOf(c.CoatingRefId))
            .ToLookup(c => c.LensOptionId, c => c.CoatingRefId);
        var lensPairings = (await dbContext.LensOptionCoatingPairings.ToListAsync(cancellationToken))
            .OrderBy(p => PositionOf(p.TriggerCoatingRefId)).ThenBy(p => PositionOf(p.PairedCoatingRefId))
            .ToLookup(p => p.LensOptionId, p => new CoatingPairingRule(p.TriggerCoatingRefId, p.PairedCoatingRefId));
        var exclusions = await dbContext.CoatingExclusions.ToListAsync(cancellationToken);
        var assignments = await dbContext.PresetCatalogueAssignments.ToListAsync(cancellationToken);
        var orgPaths = (await unscopedReportQueryService.GetOrganisationNodePathsUnscopedAsync(cancellationToken))
            .ToDictionary(x => x.Id, x => x.HierarchyPath);

        // An assignment to a deactivated org has no path here (the unscoped query leaves deleted
        // orgs out), so it reaches nothing — nobody can be making a record there.
        var assignedPathsByCatalogue = assignments
            .Where(a => orgPaths.ContainsKey(a.OrgNodeId))
            .GroupBy(a => a.PresetCatalogueId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(a => orgPaths[a.OrgNodeId]).ToList());

        _snapshot = new ReferenceDataSnapshot(
            items.Select(x => new ReferenceItemSnapshot(x.Id, x.Category.ToContract(), x.Label, x.IsActive, x.IsOtherOption)).ToList(),
            catalogues.Select(c => new PresetCatalogueSnapshot(
                c.Id,
                c.Name,
                IsActive: !c.IsDeleted,
                lensOptions.Where(l => l.PresetCatalogueId == c.Id)
                    .Select(l => new LensOptionSnapshot(
                        l.Id, l.Label, l.Sphere, lensCoatings[l.Id].ToList(),
                        l.Cylinder, l.Axis, l.Add, l.LensTypeRefId, l.LensTypeOtherText,
                        lensPairings[l.Id].ToList()))
                    .ToList(),
                AssignedOrgPaths: assignedPathsByCatalogue.GetValueOrDefault(c.Id, []))).ToList(),
            exclusions.Select(e => new CoatingExclusionRule(e.CoatingRefIdA, e.CoatingRefIdB)).ToList());

        return _snapshot;
    }
}

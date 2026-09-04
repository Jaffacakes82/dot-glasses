using DotGlasses.Application.Common;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Rules.ReferenceData;
using Microsoft.EntityFrameworkCore;
using ContractPresetCatalogueKind = DotGlasses.Contracts.Common.PresetCatalogueKind;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>
/// Queries DotGlassesDbContext directly rather than through a repository — matches
/// ReferenceDataQueryService/PresetCatalogueQueryService.
///
/// No IsActive filter anywhere: this is the copy that has to carry retired items, both so a
/// historical record still renders its label and so a rule can tell "retired" (present, inactive
/// — reject, and say why) apart from "never existed". None of the tables read here are
/// hierarchy-scoped — reference data and preset catalogues are a single global library visible to
/// every authenticated user (see CLAUDE.md), so there is no caller to scope by.
///
/// Memoized for the lifetime of the scope, i.e. one request. Every Admin Portal action that
/// mutates reference data redirects rather than re-rendering, so nothing reads a snapshot it
/// invalidated earlier in the same request; if a future action ever writes and then renders a list
/// in one request, it needs its own read rather than this.
/// </summary>
public class ReferenceDataSnapshotProvider(DotGlassesDbContext dbContext) : IReferenceDataSnapshotProvider
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

        var catalogues = await dbContext.PresetCatalogues.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var lensOptions = await dbContext.LensOptions.OrderBy(l => l.SortOrder).ToListAsync(cancellationToken);
        var coatingAvailability = await dbContext.LensStrengthCoatingOptions.ToListAsync(cancellationToken);
        var pairings = await dbContext.CoatingPairings.ToListAsync(cancellationToken);
        var exclusions = await dbContext.CoatingExclusions.ToListAsync(cancellationToken);

        var labelsByRefId = items.ToDictionary(x => x.Id, x => x.Label);
        var availableCoatingsByStrength = coatingAvailability
            .GroupBy(o => o.LensStrengthRefId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(o => o.CoatingRefId).ToList());

        _snapshot = new ReferenceDataSnapshot(
            items.Select(x => new ReferenceItemSnapshot(x.Id, x.Category.ToContract(), x.Label, x.IsActive, x.IsOtherOption)).ToList(),
            catalogues.Select(c => new PresetCatalogueSnapshot(
                c.Id,
                c.Name,
                (ContractPresetCatalogueKind)(int)c.Kind,
                lensOptions.Where(l => l.PresetCatalogueId == c.Id)
                    .Select(l => new LensOptionSnapshot(
                        l.Id,
                        labelsByRefId.GetValueOrDefault(l.LensStrengthRefId, ReferenceDataSnapshot.MissingLabel),
                        l.SortOrder,
                        availableCoatingsByStrength.GetValueOrDefault(l.LensStrengthRefId, [])))
                    .ToList())).ToList(),
            pairings.Select(p => new CoatingPairingRule(p.TriggerCoatingRefId, p.PairedCoatingRefId)).ToList(),
            exclusions.Select(e => new CoatingExclusionRule(e.CoatingRefIdA, e.CoatingRefIdB)).ToList());

        return _snapshot;
    }
}

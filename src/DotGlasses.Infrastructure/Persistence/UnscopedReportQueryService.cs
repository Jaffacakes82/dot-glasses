using DotGlasses.Application.Reporting;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class UnscopedReportQueryService(DotGlassesDbContext dbContext) : IUnscopedReportQueryService
{
    // IgnoreQueryFilters() disables the combined hierarchy+soft-delete filter wholesale (EF Core
    // has no per-concern opt-out), so soft-delete is re-applied explicitly on every query here —
    // "unscoped" means "outside the caller's hierarchy", not "including deleted rows".
    public async Task<IReadOnlyList<OrganisationNodePath>> GetOrganisationNodePathsUnscopedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .Select(x => new OrganisationNodePath(x.Id, x.HierarchyPath))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OrganisationNodeSummary>> GetOrganisationNodesUnscopedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .Select(x => new OrganisationNodeSummary(x.Id, x.Name, x.Level, x.HierarchyPath, x.IsTrainingOrg, false))
            .ToListAsync(cancellationToken);

    // The one query here that does include soft-deleted rows, on purpose and flagged: a
    // deactivated organisation's records still belong to it in the reports.
    public async Task<IReadOnlyList<OrganisationNodeSummary>> GetOrganisationNodesForReportsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Select(x => new OrganisationNodeSummary(x.Id, x.Name, x.Level, x.HierarchyPath, x.IsTrainingOrg, x.IsDeleted))
            .ToListAsync(cancellationToken);
}

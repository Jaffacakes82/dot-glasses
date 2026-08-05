using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

public class UnscopedReportQueryService(DotGlassesDbContext dbContext) : IUnscopedReportQueryService
{
    public async Task<IReadOnlyList<WidgetExample>> GetAllWidgetExamplesUnscopedAsync(CancellationToken cancellationToken = default) =>
        // IgnoreQueryFilters() disables the combined hierarchy+soft-delete filter wholesale
        // (EF Core has no per-concern opt-out), so soft-delete is re-applied explicitly here —
        // "unscoped" means "outside the caller's hierarchy", not "including deleted rows".
        await dbContext.WidgetExamples
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.HierarchyPath)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

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
            .Select(x => new OrganisationNodeSummary(x.Id, x.Name, x.Level, x.HierarchyPath, x.IsTrainingOrg))
            .ToListAsync(cancellationToken);
}

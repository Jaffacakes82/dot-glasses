using DotGlasses.Application.Organisations;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — no
/// repository interface exists for OrganisationNode, matching OrganisationAdminService.</summary>
public class OrganisationNodeLookup(DotGlassesDbContext dbContext) : IOrganisationNodeLookup
{
    public async Task<OrganisationNodeStatus?> FindByHierarchyPathAsync(string hierarchyPath, CancellationToken cancellationToken = default) =>
        await dbContext.OrganisationNodes
            .IgnoreQueryFilters()
            .Where(x => x.HierarchyPath == hierarchyPath)
            .Select(x => new OrganisationNodeStatus(x.Id, x.Name, !x.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);
}

using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Domain.Enums;
using DotGlasses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Identity;

public class UserAssignmentsQueryService(
    DotGlassesDbContext dbContext,
    IUnscopedReportQueryService unscopedReportQueryService) : IUserAssignmentsQueryService
{
    public async Task<IReadOnlyList<UserAssignmentSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var assignedOrgIds = await AssignedOrgIdsAsync(userId, cancellationToken);
        if (assignedOrgIds.Count == 0)
        {
            return [];
        }

        var orgNodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);

        return orgNodes
            .Where(o => assignedOrgIds.Contains(o.Id))
            .Select(node => new UserAssignmentSummary(node.Name, node.Level))
            .OrderBy(a => a.Level)
            .ThenBy(a => a.Name, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<OwningOrgOption>> ListDgiOrCountryAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var assignedOrgIds = await AssignedOrgIdsAsync(userId, cancellationToken);
        if (assignedOrgIds.Count == 0)
        {
            return [];
        }

        var orgNodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);

        return orgNodes
            .Where(o => assignedOrgIds.Contains(o.Id) && o.Level is OrganisationLevel.Dgi or OrganisationLevel.Country)
            .OrderBy(o => o.Level)
            .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .Select(o => new OwningOrgOption(o.Id, o.Name))
            .ToList();
    }

    private async Task<HashSet<Guid>> AssignedOrgIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await dbContext.UserOrgAssignments
            .Where(a => a.UserId == userId)
            .Select(a => a.OrgNodeId)
            .ToListAsync(cancellationToken))
        .ToHashSet();
}

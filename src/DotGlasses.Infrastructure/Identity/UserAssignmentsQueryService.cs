using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Identity;

public class UserAssignmentsQueryService(
    DotGlassesDbContext dbContext,
    IUnscopedReportQueryService unscopedReportQueryService) : IUserAssignmentsQueryService
{
    public async Task<IReadOnlyList<UserAssignmentSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var assignedOrgIds = await dbContext.UserOrgAssignments
            .Where(a => a.UserId == userId)
            .Select(a => a.OrgNodeId)
            .ToListAsync(cancellationToken);

        // Transitional, same rule as UserAccessLoader.ReadAsync: the account's old active org
        // (ApplicationUser.OrgNodeId) still counts as one of its assignments until the migration
        // that removes that column backfills a real UserOrgAssignment row for it — otherwise an
        // account that predates ADR-0006 and has never had a row added would show no assignments
        // at all despite having real access.
        var activeOrgId = await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.OrgNodeId)
            .FirstOrDefaultAsync(cancellationToken);

        var orgIds = assignedOrgIds.ToHashSet();
        if (activeOrgId is { } activeId)
        {
            orgIds.Add(activeId);
        }

        if (orgIds.Count == 0)
        {
            return [];
        }

        // Unscoped — an assignment can sit outside the caller's own current scope (the entire
        // point of a secondary assignment), so a plain OrganisationNodes query would silently
        // drop it for anyone but a DGI-level user.
        var orgNodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);
        var byId = orgNodes.ToDictionary(o => o.Id);

        return orgIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Select(node => new UserAssignmentSummary(node.Name, node.Level))
            .OrderBy(a => a.Level)
            .ThenBy(a => a.Name, StringComparer.Ordinal)
            .ToList();
    }
}

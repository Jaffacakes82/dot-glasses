using DotGlasses.Application.Reporting;
using DotGlasses.Application.Users;
using DotGlasses.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Identity;

public class UserOrgAssignmentService(
    DotGlassesDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IUnscopedReportQueryService unscopedReportQueryService) : IUserOrgAssignmentService
{
    public async Task<IReadOnlyList<AssignedOrgSummary>> ListAssignedOrgsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        var assignments = await dbContext.UserOrgAssignments
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        // Unscoped — an assigned org can sit outside the user's own *current* active scope (that
        // is the whole point of a secondary assignment), so a plain OrganisationNodes query would
        // silently drop it for anyone but a DGI-level user.
        var orgNodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);
        var byId = orgNodes.ToDictionary(o => o.Id);

        return assignments
            .Select(a => new AssignedOrgSummary(
                a.OrgNodeId,
                byId.TryGetValue(a.OrgNodeId, out var node) ? node.Name : "Unknown",
                a.OrgNodeId == user.OrgNodeId))
            .OrderBy(o => o.Name)
            .ToList();
    }

    public async Task SwitchActiveOrgAsync(Guid userId, Guid targetOrgNodeId, CancellationToken cancellationToken = default)
    {
        var isAssigned = await dbContext.UserOrgAssignments
            .AnyAsync(a => a.UserId == userId && a.OrgNodeId == targetOrgNodeId, cancellationToken);
        if (!isAssigned)
        {
            throw new InvalidOperationException("The target org is not one of this user's assigned locations.");
        }

        // Unscoped for the same reason as ListAssignedOrgsAsync — the target org may sit outside
        // the user's own current scope (switching *to* a foreign org is exactly this feature).
        var orgNodes = await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken);
        var targetOrg = orgNodes.FirstOrDefault(o => o.Id == targetOrgNodeId)
            ?? throw new InvalidOperationException("Target org node not found.");

        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        user.OrgNodeId = targetOrg.Id;
        user.HierarchyPath = targetOrg.HierarchyPath;
        user.OrgLevel = targetOrg.Level;
        await userManager.UpdateAsync(user);
    }
}

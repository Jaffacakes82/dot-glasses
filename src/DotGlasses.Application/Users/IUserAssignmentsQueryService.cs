using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Users;

/// <summary>
/// Read-only summary of a signed-in user's own org assignments (UserOrgAssignment), each resolved
/// to its name and level — the Admin Portal sidebar footer (ticket 05) is the only consumer today.
/// Deliberately separate from IUserOrgAssignmentService, which drives the Field App's self-service
/// org-switching (AuthController's my-orgs, Settings.razor) and whose AssignedOrgSummary carries
/// no Level: widening that contract for every existing caller, none of which needs Level, is worse
/// than one small additional service.
///
/// Resolves names via IUnscopedReportQueryService, not a plain scoped query — an assignment can
/// sit outside the caller's own current scope (that's the entire point of a secondary
/// assignment), so a plain OrganisationNodes query would silently drop it for anyone but a
/// DGI-level user (see CLAUDE.md's standing ancestor-resolution gotcha).
/// </summary>
public interface IUserAssignmentsQueryService
{
    /// <summary>Every assignment the user holds, sorted by level (DGI first) then name. An
    /// assignment whose org node no longer resolves (soft-deleted) is silently dropped rather than
    /// shown as "Unknown" — nothing here is safety-critical the way scope/RBAC are, so the sidebar
    /// stays clean instead of surfacing a housekeeping detail.</summary>
    Task<IReadOnlyList<UserAssignmentSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public record UserAssignmentSummary(string Name, OrganisationLevel Level);

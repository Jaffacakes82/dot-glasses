using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Users;

/// <summary>
/// Read-only views of a user's own org assignments (UserOrgAssignment), each resolved to its name
/// — for the Admin Portal's sidebar footer, and for choosing which of an admin's own assignments
/// owns a new lens set.
///
/// Resolves names via IUnscopedReportQueryService, not a plain scoped query — an assignment can
/// sit outside the caller's own request scope (a Field App request is scoped to one location, and
/// a Dgi assignment sits above any narrower one), so a plain OrganisationNodes query would
/// silently drop it (see CLAUDE.md's standing ancestor-resolution gotcha).
/// </summary>
public interface IUserAssignmentsQueryService
{
    /// <summary>Every assignment the user holds, sorted by level (DGI first) then name. An
    /// assignment whose org node no longer resolves (soft-deleted) is silently dropped rather than
    /// shown as "Unknown" — nothing here is safety-critical the way scope/RBAC are, so the sidebar
    /// stays clean instead of surfacing a housekeeping detail.</summary>
    Task<IReadOnlyList<UserAssignmentSummary>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's own assignments at Dgi or Country level only, id+name resolved —
    /// exactly the set a new PresetCatalogue's owning org may be chosen from
    /// (PresetCatalogueAdminService.CreateAsync enforces the same Dgi/Country rule; ADR-0006's
    /// "lens set creation" note). Ordered Dgi first, then by name, matching the Organisations
    /// screen's own level-then-name ordering.</summary>
    Task<IReadOnlyList<OwningOrgOption>> ListDgiOrCountryAssignmentsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public record UserAssignmentSummary(string Name, OrganisationLevel Level);

/// <summary>A Dgi/Country-level org the user is assigned to — a candidate to own a new lens set.</summary>
public record OwningOrgOption(Guid OrgNodeId, string Name);

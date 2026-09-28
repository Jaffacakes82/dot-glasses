namespace DotGlasses.Domain.Entities;

/// <summary>
/// A user's org assignment (CONTEXT.md). On the Admin Portal a user's scope is every assignment
/// combined, re-read on every request (ADR-0006). The Field App still switches between them
/// (Settings -> "switch selling point"), with its current selection on
/// ApplicationUser.OrgNodeId/HierarchyPath (Infrastructure) for now.
/// </summary>
public class UserOrgAssignment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OrgNodeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

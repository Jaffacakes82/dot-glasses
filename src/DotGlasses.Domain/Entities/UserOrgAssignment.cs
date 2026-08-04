namespace DotGlasses.Domain.Entities;

/// <summary>
/// Which org nodes a user can switch between (Settings -> "switch selling point"). The user's
/// currently active selection lives on ApplicationUser.OrgNodeId/HierarchyPath (Infrastructure) —
/// this table is the assignable set, not the active one.
/// </summary>
public class UserOrgAssignment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OrgNodeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

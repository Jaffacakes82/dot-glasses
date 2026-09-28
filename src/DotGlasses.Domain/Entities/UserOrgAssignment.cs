namespace DotGlasses.Domain.Entities;

/// <summary>
/// A user's org assignment (CONTEXT.md). On the Admin Portal a user's scope is every assignment
/// combined, re-read on every request (ADR-0006). The Field App records at one current location:
/// an active retail point the user is directly assigned to, carried in its token only (never on
/// the user row) and re-validated against these rows on every request.
/// </summary>
public class UserOrgAssignment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid OrgNodeId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

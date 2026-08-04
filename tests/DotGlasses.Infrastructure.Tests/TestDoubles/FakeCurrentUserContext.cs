using DotGlasses.Application.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Infrastructure.Tests.TestDoubles;

public class FakeCurrentUserContext : ICurrentUserContext
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? UserName { get; set; } = "test-user";
    public Guid? OrgNodeId { get; set; }
    public string HierarchyPathPrefix { get; set; } = string.Empty;
    public OrganisationLevel? OrgLevel { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
}

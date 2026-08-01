using DotGlasses.Application.Common;

namespace DotGlasses.Infrastructure.Tests.TestDoubles;

public class FakeCurrentUserContext : ICurrentUserContext
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? UserName { get; set; } = "test-user";
    public Guid? OrgNodeId { get; set; }
    public string HierarchyPathPrefix { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Roles { get; set; } = [];
}

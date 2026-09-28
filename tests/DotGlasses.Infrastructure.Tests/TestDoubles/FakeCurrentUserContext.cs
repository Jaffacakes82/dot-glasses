using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Infrastructure.Tests.TestDoubles;

public class FakeCurrentUserContext : ICurrentUserContext
{
    public bool IsAuthenticated { get; set; } = true;
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? UserName { get; set; } = "test-user";
    public IReadOnlyList<HierarchyPath> ScopePaths { get; set; } = [];
    public OrganisationLevel? HighestLevel { get; set; }
    public string? Role { get; set; }
    public bool IsSuspended { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = [];
    public CurrentLocationCheck CurrentLocation { get; set; } = CurrentLocationCheck.NoLocation;
}

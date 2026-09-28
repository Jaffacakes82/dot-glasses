using System.Security.Claims;
using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace DotGlasses.Infrastructure.Identity;

public class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    private UserAccess Access => RequestUserAccess.Get(httpContextAccessor.HttpContext);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId => ReadUserId(Principal);

    public string? UserName => Principal?.Identity?.Name;

    public IReadOnlyList<HierarchyPath> ScopePaths => Access.ScopePaths;

    public OrganisationLevel? HighestLevel => Access.HighestLevel;

    public string? Role => Access.Role;

    public bool IsSuspended => Access.IsSuspended;

    public IReadOnlyCollection<string> Roles => Role is { } role ? [role] : [];

    public Guid? OrgNodeId =>
        Guid.TryParse(Principal?.FindFirstValue(DotGlassesClaimTypes.OrgNodeId), out var id) ? id : null;

    public string HierarchyPathPrefix => Principal?.FindFirstValue(DotGlassesClaimTypes.HierarchyPath) ?? string.Empty;

    public OrganisationLevel? OrgLevel =>
        Enum.TryParse<OrganisationLevel>(Principal?.FindFirstValue(DotGlassesClaimTypes.OrgLevel), out var level) ? level : null;

    internal static Guid? ReadUserId(ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

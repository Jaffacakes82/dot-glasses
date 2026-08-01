using System.Security.Claims;
using DotGlasses.Application.Common;
using Microsoft.AspNetCore.Http;

namespace DotGlasses.Infrastructure.Identity;

public class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? UserName => Principal?.Identity?.Name;

    public Guid? OrgNodeId =>
        Guid.TryParse(Principal?.FindFirstValue(DotGlassesClaimTypes.OrgNodeId), out var id) ? id : null;

    public string HierarchyPathPrefix => Principal?.FindFirstValue(DotGlassesClaimTypes.HierarchyPath) ?? string.Empty;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
}

using System.Security.Claims;
using DotGlasses.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DotGlasses.Infrastructure.Identity;

/// <summary>
/// Adds the OrgNodeId/HierarchyPath claims on top of Identity's defaults (NameIdentifier, Name,
/// Role). Used for both cookie sign-in (DotGlasses.Web's SignInManager) and JWT issuance
/// (DotGlasses.Web's AuthController resolves the same factory) so both auth paths build
/// identical claims from one place.
/// </summary>
public class ApplicationUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(DotGlassesClaimTypes.HierarchyPath, user.HierarchyPath));

        if (user.OrgNodeId is { } orgNodeId)
        {
            identity.AddClaim(new Claim(DotGlassesClaimTypes.OrgNodeId, orgNodeId.ToString()));
        }

        if (user.OrgLevel is { } orgLevel)
        {
            identity.AddClaim(new Claim(DotGlassesClaimTypes.OrgLevel, orgLevel.ToString()));
        }

        return identity;
    }
}

using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.Users;
using DotGlasses.Contracts.Auth;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Web.Auth;
using FluentValidation;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

/// <summary>
/// Issues JWTs for API consumers (the Field App). The Admin Portal's own browser session uses
/// cookie auth instead, via Controllers/AccountController.cs's MVC login page — both check the
/// same Identity user store. Login is the only anonymous action here — MyOrgs/SwitchOrg act on
/// the caller's own identity, so they need the class-level JWT [Authorize].
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory,
    IJwtTokenService jwtTokenService,
    IUserOrgAssignmentService userOrgAssignmentService,
    ICurrentUserContext currentUser,
    IValidator<LoginRequest> loginValidator,
    IValidator<SwitchOrgRequest> switchOrgValidator) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation.ToModelStateDictionary());
        }

        var user = await userManager.FindByNameAsync(request.UserName);
        if (user is null)
        {
            return Unauthorized();
        }

        var passwordCheck = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!passwordCheck.Succeeded)
        {
            return Unauthorized();
        }

        user.LastLoginUtc = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        var principal = await claimsPrincipalFactory.CreateAsync(user);
        var (token, expiresAtUtc) = jwtTokenService.CreateToken(principal.Claims);

        return Ok(new LoginResponse { AccessToken = token, ExpiresAtUtc = expiresAtUtc });
    }

    /// <summary>The caller's own assignable locations (UserOrgAssignment), for Settings.razor's
    /// location list and OutletSelect.razor's post-login picker. IsActive marks whichever one is
    /// currently ApplicationUser.OrgNodeId.</summary>
    [HttpGet("my-orgs")]
    public async Task<ActionResult<IReadOnlyList<AssignedOrgDto>>> MyOrgs(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var orgs = await userOrgAssignmentService.ListAssignedOrgsAsync(userId, cancellationToken);
        return Ok(orgs.Select(o => new AssignedOrgDto { OrgNodeId = o.OrgNodeId, Name = o.Name, IsActive = o.IsActive }).ToList());
    }

    /// <summary>Switches the caller's active selling point to one of their own assigned orgs and
    /// returns a freshly-minted JWT carrying the new HierarchyPath/OrgNodeId/OrgLevel claims — the
    /// old token is still valid until it naturally expires (no server-side revocation exists), so
    /// the client must swap in the new token immediately, not just treat 200 as confirmation.</summary>
    [HttpPost("switch-org")]
    public async Task<ActionResult<LoginResponse>> SwitchOrg(SwitchOrgRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var validation = await switchOrgValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation.ToModelStateDictionary());
        }

        // A rejection here ("not one of this user's assigned locations") is a
        // DomainRuleViolationException, turned into a 400 ValidationProblemDetails by
        // DomainRuleViolationFilter — same shape the validator failure above returns (ADR-0003).
        await userOrgAssignmentService.SwitchActiveOrgAsync(userId, request.OrgNodeId, cancellationToken);

        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        var principal = await claimsPrincipalFactory.CreateAsync(user);
        var (token, expiresAtUtc) = jwtTokenService.CreateToken(principal.Claims);

        return Ok(new LoginResponse { AccessToken = token, ExpiresAtUtc = expiresAtUtc });
    }
}

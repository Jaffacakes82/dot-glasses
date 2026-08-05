using Asp.Versioning;
using DotGlasses.Contracts.Auth;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Web.Auth;
using FluentValidation;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers.Api.V1;

/// <summary>
/// Issues JWTs for API consumers (the Field App). The Admin Portal's own browser session uses
/// cookie auth instead, via Controllers/AccountController.cs's MVC login page — both check the
/// same Identity user store.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsPrincipalFactory,
    IJwtTokenService jwtTokenService,
    IValidator<LoginRequest> loginValidator) : ControllerBase
{
    [HttpPost("login")]
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
}

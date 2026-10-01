using System.Security.Claims;
using Asp.Versioning;
using DotGlasses.Application.Common;
using DotGlasses.Application.Users;
using DotGlasses.Contracts.Auth;
using DotGlasses.Domain.Common;
using DotGlasses.Infrastructure.Identity;
using DotGlasses.Web.Auth;
using FluentValidation;
using DotGlasses.Web.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DotGlasses.Web.Controllers.Api.V1;

/// <summary>
/// Issues JWTs for API consumers (the Field App). The Admin Portal's own browser session uses
/// cookie auth instead, via Controllers/AccountController.cs's MVC login page — both check the
/// same Identity user store. Login is the only anonymous action here — MyOrgs/SwitchOrg act on
/// the caller's own identity, so they need the class-level JWT [Authorize]. Each token carries the
/// Field App's current location (ADR-0006), chosen here among the user's eligible locations.
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
    IUserAccessLoader userAccessLoader,
    ICurrentUserContext currentUser,
    IValidator<LoginRequest> loginValidator,
    IValidator<SwitchOrgRequest> switchOrgValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    PasswordResetRequester passwordResetRequester) : ControllerBase
{
    /// <summary>The Field App's "Forgot password?". Anonymous, and it answers the same way for
    /// every address — known, unknown, suspended, or emailed a moment ago — so it can't be used
    /// to find out who has an account. The link itself is only ever emailed.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await passwordResetRequester.RequestAsync(HttpContext, request.Email, RequestingApp.FieldApp, cancellationToken);
        return Ok(new ForgotPasswordResponse { Message = IPasswordResetService.Acknowledgement });
    }

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

        // The device's remembered location if it is still eligible, else the only eligible one,
        // else none — the Field App then asks the technician to pick.
        var eligible = await userAccessLoader.ListEligibleLocationsAsync(user.Id, cancellationToken);
        var location = eligible.FirstOrDefault(l => l.OrgNodeId == request.PreferredLocationId)
            ?? (eligible.Count == 1 ? eligible[0] : null);

        return Ok(await IssueTokenAsync(user, location));
    }

    /// <summary>The caller's eligible locations — the active retail points they are directly
    /// assigned to — for Settings.razor's location list and OutletSelect.razor's post-login
    /// picker. IsActive marks the token's current location, when it is still valid.</summary>
    [HttpGet("my-orgs")]
    public async Task<ActionResult<IReadOnlyList<AssignedOrgDto>>> MyOrgs(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var current = currentUser.CurrentLocation.ValidLocation?.OrgNodeId;
        var eligible = await userAccessLoader.ListEligibleLocationsAsync(userId, cancellationToken);
        return Ok(eligible.Select(l => new AssignedOrgDto { OrgNodeId = l.OrgNodeId, Name = l.Name, IsActive = l.OrgNodeId == current }).ToList());
    }

    /// <summary>Switches the caller's current location to another of their eligible locations and
    /// returns a freshly-minted JWT carrying it. The location lives only in the token — nothing is
    /// written to the user row, so two devices on one account keep a location each. The old token
    /// is still valid until it naturally expires (no server-side revocation exists), so the client
    /// must swap in the new token immediately, not just treat 200 as confirmation.</summary>
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

        var eligible = await userAccessLoader.ListEligibleLocationsAsync(userId, cancellationToken);

        // Rendered as a 400 ValidationProblemDetails keyed on "" by DomainRuleViolationFilter —
        // the same shape the validator failure above returns (ADR-0003).
        var location = eligible.FirstOrDefault(l => l.OrgNodeId == request.OrgNodeId)
            ?? throw new DomainRuleViolationException("That isn't an active retail point you're assigned to.");

        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        return Ok(await IssueTokenAsync(user, location));
    }

    /// <summary>Changes the caller's own password. No fresh token is issued — a password change
    /// touches no JWT claim, and there's no server-side revocation to invalidate the old one
    /// either way, same reasoning as SwitchOrg's doc comment for why that one *does* reissue.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation.ToModelStateDictionary());
        }

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var modelState = new ModelStateDictionary();
            foreach (var error in result.Errors)
            {
                // PasswordMismatch is the only IdentityResult error about the *current* password;
                // every other code (PasswordTooShort, PasswordRequiresNonAlphanumeric, ...) is
                // about the new one failing the configured policy.
                var field = error.Code == "PasswordMismatch"
                    ? nameof(ChangePasswordRequest.CurrentPassword)
                    : nameof(ChangePasswordRequest.NewPassword);
                modelState.AddModelError(field, error.Description);
            }

            return ValidationProblem(modelState);
        }

        return Ok();
    }

    /// <summary>A Field App token: Identity's own claims (who the user is) plus the current
    /// location, if any — only named here, and re-validated on every request (AccessRecheck).</summary>
    private async Task<LoginResponse> IssueTokenAsync(ApplicationUser user, CurrentLocation? location)
    {
        var principal = await claimsPrincipalFactory.CreateAsync(user);
        var claims = principal.Claims.ToList();
        if (location is not null)
        {
            claims.Add(new Claim(DotGlassesClaimTypes.CurrentLocationId, location.OrgNodeId.ToString()));
        }

        var (token, expiresAtUtc) = jwtTokenService.CreateToken(claims);
        return new LoginResponse
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
            DisplayName = user.DisplayName(fallback: string.Empty),
            CurrentLocationId = location?.OrgNodeId,
            CurrentLocationName = location?.Name,
        };
    }
}

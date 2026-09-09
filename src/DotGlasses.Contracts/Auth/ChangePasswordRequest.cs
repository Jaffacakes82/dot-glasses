using FluentValidation;

namespace DotGlasses.Contracts.Auth;

/// <summary>
/// Posted to POST /api/v1/auth/change-password by an already-authenticated (JWT) caller to change
/// their own password — the API counterpart to the Admin Portal's cookie-based
/// AccountController.ChangePassword. No fresh token is issued in response: a password change
/// touches no JWT claim, and there is no server-side token revocation to invalidate the old one
/// either way (see AuthController.SwitchOrg's own doc comment).
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty();
    }
}

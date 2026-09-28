using FluentValidation;

namespace DotGlasses.Contracts.Auth;

/// <summary>
/// Posted to POST /api/v1/auth/login by the Field App (and any other API consumer) to obtain a
/// JWT. The Admin Portal's own browser session instead uses cookie auth via the MVC
/// Account/Login page — this endpoint exists for API/App consumers specifically.
/// </summary>
public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>The current location this device remembers, if any. Used when it is still one of
    /// the user's eligible locations (an active retail point they are directly assigned to);
    /// otherwise the token carries the only eligible location if there is exactly one, or none.</summary>
    public Guid? PreferredLocationId { get; set; }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

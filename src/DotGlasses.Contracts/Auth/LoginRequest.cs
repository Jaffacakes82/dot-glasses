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
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

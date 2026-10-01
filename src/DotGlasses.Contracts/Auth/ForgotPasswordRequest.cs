namespace DotGlasses.Contracts.Auth;

/// <summary>Posted anonymously to POST /api/v1/auth/forgot-password by the Field App's "Forgot
/// password?" screen. The response is the same whatever the address: whether it has an account is
/// never disclosed, and the reset link is only ever emailed.</summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordResponse
{
    public string Message { get; set; } = string.Empty;
}

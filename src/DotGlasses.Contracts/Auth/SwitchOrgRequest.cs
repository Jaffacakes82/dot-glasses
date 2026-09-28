using FluentValidation;

namespace DotGlasses.Contracts.Auth;

/// <summary>
/// Posted to POST /api/v1/auth/switch-org to change the caller's current location to another of
/// their eligible locations. The response is a fresh LoginResponse — the current location lives
/// only in the token (never on the user row), so the client must swap in the new token, not just
/// accept a 200.
/// </summary>
public class SwitchOrgRequest
{
    public Guid OrgNodeId { get; set; }
}

public class SwitchOrgRequestValidator : AbstractValidator<SwitchOrgRequest>
{
    public SwitchOrgRequestValidator()
    {
        RuleFor(x => x.OrgNodeId).NotEmpty();
    }
}

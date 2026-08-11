using FluentValidation;

namespace DotGlasses.Contracts.Auth;

/// <summary>
/// Posted to POST /api/v1/auth/switch-org to change the caller's active selling point among
/// their own assigned locations (see UserOrgAssignment's doc comment). The response is a fresh
/// LoginResponse — switching changes what the JWT's HierarchyPath/OrgNodeId/OrgLevel claims say,
/// so the client must swap in the new token, not just accept a 200.
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

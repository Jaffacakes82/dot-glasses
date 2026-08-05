using DotGlasses.Application.Common;
using DotGlasses.Application.Organisations;
using DotGlasses.Application.Users;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.UserDirectory;

/// <summary>Lives in Web, not Contracts — same reasoning as every other DB-backed validator
/// this session: needs checks (email not already taken, every org in the caller's own scope)
/// that can't be co-located with a Contracts DTO, and this request isn't Contracts-shaped
/// anyway.</summary>
public class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator(IUserAdminService userAdminService, IOrganisationAdminService organisationAdminService)
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role).Must(role => RoleNames.All.Contains(role)).WithMessage("Role must be one of: " + string.Join(", ", RoleNames.All));
        RuleFor(x => x.OrgNodeIds).NotEmpty().WithMessage("At least one location must be assigned.");

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            if (!string.IsNullOrEmpty(request.Email) && await userAdminService.EmailExistsAsync(request.Email, cancellationToken))
            {
                context.AddFailure(nameof(request.Email), "A user with this email already exists.");
            }

            if (request.OrgNodeIds.Count > 0)
            {
                // The picker only ever renders orgs from IOrganisationAdminService.ListAsync(),
                // which is already scoped to the caller — this re-check is defense in depth, same
                // principle as every other write action wired so far (never trust the hidden-
                // option UX alone).
                var visibleOrgIds = (await organisationAdminService.ListAsync(cancellationToken)).Select(n => n.Id).ToHashSet();
                if (request.OrgNodeIds.Any(id => !visibleOrgIds.Contains(id)))
                {
                    context.AddFailure(nameof(request.OrgNodeIds), "One or more selected locations are outside your own scope.");
                }
            }
        });
    }
}

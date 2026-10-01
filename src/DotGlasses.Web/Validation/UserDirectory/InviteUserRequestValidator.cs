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
        RuleFor(x => x.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter the person's email address.")
            .EmailAddress().WithMessage("Enter a full email address, like name@example.com.")
            .MaximumLength(256).WithMessage("Keep the email address to 256 characters or fewer.");
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Enter the person's full name.")
            .MaximumLength(200).WithMessage("Keep the full name to 200 characters or fewer.");
        RuleFor(x => x.Role).Must(role => RoleNames.All.Contains(role)).WithMessage("Choose a role.");
        RuleFor(x => x.OrgNodeIds).NotEmpty().WithMessage("Choose at least one organisation.");

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            if (!string.IsNullOrEmpty(request.Email) && await userAdminService.EmailExistsAsync(request.Email, cancellationToken))
            {
                context.AddFailure(nameof(request.Email), "Someone already has an account with this email address.");
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
                    context.AddFailure(nameof(request.OrgNodeIds), "One of the chosen organisations is outside the ones you manage. Choose again.");
                }
            }
        });
    }
}

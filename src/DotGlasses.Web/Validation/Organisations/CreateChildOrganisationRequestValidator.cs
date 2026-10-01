using DotGlasses.Application.Organisations;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.Organisations;

/// <summary>Lives in Web, not Contracts — same reasoning as CreateReferenceDataItemRequestValidator:
/// needs a DB-backed check (does the parent exist, is this a valid level transition) that can't
/// be co-located with a Contracts DTO, and this request isn't Contracts-shaped anyway.</summary>
public class CreateChildOrganisationRequestValidator : AbstractValidator<CreateChildOrganisationRequest>
{
    public CreateChildOrganisationRequestValidator(IOrganisationAdminService organisationAdminService)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Enter a name for the organisation.")
            .MaximumLength(200).WithMessage("Keep the name to 200 characters or fewer.");
        RuleFor(x => x.Level).IsInEnum().WithMessage("Choose a level from the list.");
        RuleFor(x => x.Kind).MaximumLength(100).WithMessage("Keep the kind to 100 characters or fewer.");

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            var nodes = await organisationAdminService.ListAsync(cancellationToken);
            var parent = nodes.FirstOrDefault(n => n.Id == request.ParentId);
            if (parent is null)
            {
                context.AddFailure(nameof(request.ParentId), "The organisation this one sits under can't be found. Reload the page and try again.");
                return;
            }

            if (!organisationAdminService.IsValidChildLevel(parent.Level, request.Level))
            {
                context.AddFailure(nameof(request.Level), "That level can't sit directly under this organisation. Choose another level.");
            }
        });
    }
}

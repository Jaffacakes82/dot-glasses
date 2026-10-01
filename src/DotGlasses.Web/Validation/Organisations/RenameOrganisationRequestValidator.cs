using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.Organisations;

public class RenameOrganisationRequestValidator : AbstractValidator<RenameOrganisationRequest>
{
    public RenameOrganisationRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("That organisation can't be found. Reload the page and try again.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Enter a name for the organisation.")
            .MaximumLength(200).WithMessage("Keep the name to 200 characters or fewer.");
    }
}

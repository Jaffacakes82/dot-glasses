using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.Organisations;

public class RenameOrganisationRequestValidator : AbstractValidator<RenameOrganisationRequest>
{
    public RenameOrganisationRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

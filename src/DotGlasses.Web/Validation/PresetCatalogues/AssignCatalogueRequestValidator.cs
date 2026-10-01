using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class AssignCatalogueRequestValidator : AbstractValidator<AssignCatalogueRequest>
{
    public AssignCatalogueRequestValidator()
    {
        RuleFor(x => x.OrgNodeId).NotEmpty().WithMessage("Choose an organisation to assign this lens set to.");
    }
}

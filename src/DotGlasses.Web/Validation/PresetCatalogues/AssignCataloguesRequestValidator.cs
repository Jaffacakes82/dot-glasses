using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class AssignCataloguesRequestValidator : AbstractValidator<AssignCataloguesRequest>
{
    public AssignCataloguesRequestValidator()
    {
        RuleFor(x => x.OrgNodeId).NotEmpty();
        RuleFor(x => x.CatalogueIds).NotEmpty().WithMessage("Select at least one catalogue to assign.");
    }
}

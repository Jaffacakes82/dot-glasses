using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class CreateCatalogueRequestValidator : AbstractValidator<CreateCatalogueRequest>
{
    public CreateCatalogueRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.RangeDescription).MaximumLength(100);
    }
}

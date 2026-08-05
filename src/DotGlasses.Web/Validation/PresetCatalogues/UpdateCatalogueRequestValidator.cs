using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class UpdateCatalogueRequestValidator : AbstractValidator<UpdateCatalogueRequest>
{
    public UpdateCatalogueRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.RangeDescription).MaximumLength(100);
    }
}

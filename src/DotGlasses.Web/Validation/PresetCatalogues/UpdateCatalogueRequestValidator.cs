using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class UpdateCatalogueRequestValidator : AbstractValidator<UpdateCatalogueRequest>
{
    public UpdateCatalogueRequestValidator(IPresetCatalogueAdminService catalogueAdminService)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200)
            .MustAsync(async (request, name, cancellationToken) => !await catalogueAdminService.IsNameTakenAsync(name, request.Id, cancellationToken))
            .WithMessage(LensSetNameMessages.Taken);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.RangeDescription).MaximumLength(100);
    }
}

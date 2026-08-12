using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class UpdateCatalogueRequestValidator : AbstractValidator<UpdateCatalogueRequest>
{
    public UpdateCatalogueRequestValidator(IPresetCatalogueAdminService catalogueAdminService)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.RangeDescription).MaximumLength(100);
        RuleFor(x => x.Kind).IsInEnum();

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            if (await catalogueAdminService.HasCatalogueWithKindAsync(request.Kind, request.Id, cancellationToken))
            {
                context.AddFailure(nameof(request.Kind), $"Another catalogue is already set as {request.Kind} — only one catalogue may hold that kind.");
            }
        });
    }
}

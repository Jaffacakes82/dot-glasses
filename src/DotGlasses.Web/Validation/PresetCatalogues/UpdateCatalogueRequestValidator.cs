using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class UpdateCatalogueRequestValidator : AbstractValidator<UpdateCatalogueRequest>
{
    public UpdateCatalogueRequestValidator(IPresetCatalogueAdminService catalogueAdminService)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("That lens set can't be found. Reload the page and try again.");
        // Cascade stop: the uniqueness check has nothing to look up on a blank or overlong name.
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(LensSetNameMessages.Missing)
            .MaximumLength(200).WithMessage(LensSetNameMessages.TooLong)
            .MustAsync(async (request, name, cancellationToken) => !await catalogueAdminService.IsNameTakenAsync(name, request.Id, cancellationToken))
            .WithMessage(LensSetNameMessages.Taken);
        RuleFor(x => x.Description).MaximumLength(500).WithMessage(LensSetNameMessages.DescriptionTooLong);
    }
}

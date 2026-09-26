using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class CreateCatalogueRequestValidator : AbstractValidator<CreateCatalogueRequest>
{
    public CreateCatalogueRequestValidator(IPresetCatalogueAdminService catalogueAdminService)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200)
            .MustAsync(async (name, cancellationToken) => !await catalogueAdminService.IsNameTakenAsync(name, excludeId: null, cancellationToken))
            .WithMessage(LensSetNameMessages.Taken);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

/// <summary>Shared by the create and update validators — one sentence for one rule.</summary>
public static class LensSetNameMessages
{
    public const string Taken = "A lens set with this name already exists.";
}

using DotGlasses.Application.ReferenceData;
using DotGlasses.Web.Models;
using FluentValidation;
using ReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class AddLensOptionRequestValidator : AbstractValidator<AddLensOptionRequest>
{
    public AddLensOptionRequestValidator(IReferenceDataLookupService referenceData)
    {
        RuleFor(x => x.CatalogueId).NotEmpty();

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            var lookup = await referenceData.LookupAsync(request.LensStrengthRefId, ReferenceDataCategory.LensStrength, cancellationToken);
            if (lookup is not { IsActive: true })
            {
                context.AddFailure(nameof(request.LensStrengthRefId), "LensStrengthRefId must reference an existing, active LensStrength reference-data item.");
            }
        });
    }
}

using DotGlasses.Application.ReferenceData;
using DotGlasses.Web.Models;
using FluentValidation;
using ReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class SetCoatingAvailabilityRequestValidator : AbstractValidator<SetCoatingAvailabilityRequest>
{
    public SetCoatingAvailabilityRequestValidator(IReferenceDataLookupService referenceData)
    {
        RuleFor(x => x.LensStrengthRefId).NotEmpty();

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            var lookup = await referenceData.LookupAsync(request.CoatingRefId, ReferenceDataCategory.Coating, cancellationToken);
            if (lookup is not { IsActive: true })
            {
                context.AddFailure(nameof(request.CoatingRefId), "CoatingRefId must reference an existing, active Coating reference-data item.");
            }
        });
    }
}

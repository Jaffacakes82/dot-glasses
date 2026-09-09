using DotGlasses.Application.ReferenceData;
using DotGlasses.Web.Models;
using FluentValidation;
using ReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class SetCoatingAvailabilityBatchRequestValidator : AbstractValidator<SetCoatingAvailabilityBatchRequest>
{
    public SetCoatingAvailabilityBatchRequestValidator(IReferenceDataLookupService referenceData)
    {
        RuleForEach(x => x.Selected)
            .Must(value => SetCoatingAvailabilityBatchRequest.TryParsePair(value, out _, out _))
            .WithMessage("Each selection must be a valid lens-strength/coating pairing.");

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            foreach (var raw in request.Selected)
            {
                if (!SetCoatingAvailabilityBatchRequest.TryParsePair(raw, out _, out var coatingRefId))
                {
                    continue;
                }

                var lookup = await referenceData.LookupAsync(coatingRefId, ReferenceDataCategory.Coating, cancellationToken);
                if (lookup is not { IsActive: true })
                {
                    context.AddFailure(nameof(request.Selected), "Selection references a coating that is not an existing, active Coating reference-data item.");
                }
            }
        });
    }
}

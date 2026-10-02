using DotGlasses.Application.ReferenceData;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.ReferenceData;

public class UpdateReferenceDataItemRequestValidator : AbstractValidator<UpdateReferenceDataItemRequest>
{
    public UpdateReferenceDataItemRequestValidator(IReferenceDataAdminService referenceDataAdminService)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("That option can't be found. Reload the page and try again.");
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Enter a label for the option.")
            .MaximumLength(200).WithMessage("Keep the label to 200 characters or fewer.");

        RuleFor(x => x.Picture).CustomAsync(async (picture, context, cancellationToken) =>
        {
            if (picture is null)
            {
                return;
            }

            // Read through the admin service, not the memoised snapshot: this runs inside a write
            // to reference data (CLAUDE.md).
            var item = (await referenceDataAdminService.ListAllAsync(cancellationToken)).FirstOrDefault(x => x.Id == context.InstanceToValidate.Id);
            if (item is null || !ReferenceDataPictureUpload.TakesPictures(item.Category))
            {
                context.AddFailure(ReferenceDataPictureUpload.WrongListMessage);
            }
            else if ((await ReferenceDataPictureUpload.CheckAsync(picture, cancellationToken)).Error is { } error)
            {
                context.AddFailure(error);
            }
        });
    }
}

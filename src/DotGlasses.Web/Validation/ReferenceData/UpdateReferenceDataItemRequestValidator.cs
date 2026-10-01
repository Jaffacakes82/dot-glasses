using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.ReferenceData;

public class UpdateReferenceDataItemRequestValidator : AbstractValidator<UpdateReferenceDataItemRequest>
{
    public UpdateReferenceDataItemRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("That option can't be found. Reload the page and try again.");
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Enter a label for the option.")
            .MaximumLength(200).WithMessage("Keep the label to 200 characters or fewer.");
        RuleFor(x => x.ImageUrl).MaximumLength(2000).WithMessage("Keep the image address to 2000 characters or fewer.");
    }
}

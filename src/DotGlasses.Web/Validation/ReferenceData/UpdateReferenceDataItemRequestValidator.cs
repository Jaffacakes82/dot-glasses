using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.ReferenceData;

public class UpdateReferenceDataItemRequestValidator : AbstractValidator<UpdateReferenceDataItemRequest>
{
    public UpdateReferenceDataItemRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ImageUrl).MaximumLength(2000);
    }
}

using DotGlasses.Application.ReferenceData;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.ReferenceData;

/// <summary>Lives in Web, not Contracts — it needs a DB-backed check (is there already an active
/// "Other" item in this category) that can't be co-located with a Contracts DTO, because Contracts
/// may not reference Application. This request isn't Contracts-shaped anyway; it's MVC-only.
///
/// It is also one of the async rules ADR-0002 keeps FluentValidation around for: it writes to the
/// reference-data library, so it must not read the memoized per-request snapshot the consultation
/// rules use.</summary>
public class CreateReferenceDataItemRequestValidator : AbstractValidator<CreateReferenceDataItemRequest>
{
    public CreateReferenceDataItemRequestValidator(IReferenceDataAdminService referenceDataAdminService)
    {
        RuleFor(x => x.Category).IsInEnum().WithMessage("Choose a list to add this option to.");
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Enter a label for the option.")
            .MaximumLength(200).WithMessage("Keep the label to 200 characters or fewer.");
        RuleFor(x => x.ImageUrl).MaximumLength(2000).WithMessage("Keep the image address to 2000 characters or fewer.");

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            if (request.IsOtherOption && await referenceDataAdminService.HasActiveOtherOptionAsync(request.Category, cancellationToken))
            {
                context.AddFailure(nameof(request.IsOtherOption), "This list already has an \"Other\" option. Retire that one first.");
            }
        });
    }
}

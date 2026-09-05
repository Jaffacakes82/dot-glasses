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
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ImageUrl).MaximumLength(2000);

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            if (request.IsOtherOption && await referenceDataAdminService.HasActiveOtherOptionAsync(request.Category, cancellationToken))
            {
                context.AddFailure(nameof(request.IsOtherOption), "This category already has an active \"Other\" option — retire it first.");
            }
        });
    }
}

using DotGlasses.Application.Organisations;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.Organisations;

/// <summary>Lives in Web, not Contracts — same reasoning as CreateReferenceDataItemRequestValidator:
/// needs a DB-backed check (does the parent exist, is this a valid level transition) that can't
/// be co-located with a Contracts DTO, and this request isn't Contracts-shaped anyway.</summary>
public class CreateChildOrganisationRequestValidator : AbstractValidator<CreateChildOrganisationRequest>
{
    public CreateChildOrganisationRequestValidator(IOrganisationAdminService organisationAdminService)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Level).IsInEnum();
        RuleFor(x => x.Kind).MaximumLength(100);

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            var nodes = await organisationAdminService.ListAsync(cancellationToken);
            var parent = nodes.FirstOrDefault(n => n.Id == request.ParentId);
            if (parent is null)
            {
                context.AddFailure(nameof(request.ParentId), "ParentId must reference an existing, visible organisation node.");
                return;
            }

            if (!organisationAdminService.IsValidChildLevel(parent.Level, request.Level))
            {
                context.AddFailure(nameof(request.Level), $"{request.Level} is not a valid child level under a {parent.Level} node.");
            }
        });
    }
}

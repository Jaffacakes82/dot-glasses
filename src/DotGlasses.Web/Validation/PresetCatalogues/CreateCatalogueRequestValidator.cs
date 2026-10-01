using DotGlasses.Application.Common;
using DotGlasses.Application.PresetCatalogues;
using DotGlasses.Application.Users;
using DotGlasses.Web.Models;
using FluentValidation;

namespace DotGlasses.Web.Validation.PresetCatalogues;

public class CreateCatalogueRequestValidator : AbstractValidator<CreateCatalogueRequest>
{
    public CreateCatalogueRequestValidator(
        IPresetCatalogueAdminService catalogueAdminService,
        IUserAssignmentsQueryService userAssignmentsQueryService,
        ICurrentUserContext currentUserContext)
    {
        // Cascade stop: the uniqueness check has nothing to look up on a blank or overlong name.
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(LensSetNameMessages.Missing)
            .MaximumLength(200).WithMessage(LensSetNameMessages.TooLong)
            .MustAsync(async (name, cancellationToken) => !await catalogueAdminService.IsNameTakenAsync(name, excludeId: null, cancellationToken))
            .WithMessage(LensSetNameMessages.Taken);
        RuleFor(x => x.Description).MaximumLength(500).WithMessage(LensSetNameMessages.DescriptionTooLong);

        // The owning org is chosen from the caller's own Dgi/Country assignments (spec user
        // stories 20-21), never re-derived from a client-submitted path. A chosen org outside
        // that set is refused; a blank choice is only legal when exactly one assignment
        // qualifies — the form omits the field in that case and expects it to be picked
        // automatically (CataloguesController.ResolveOwningOrgNodeIdAsync), so an admin with
        // several qualifying assignments who somehow posts no choice is refused rather than
        // guessed for.
        RuleFor(x => x.OwningOrgNodeId)
            .MustAsync(async (request, ownerId, cancellationToken) =>
            {
                if (currentUserContext.UserId is not { } userId)
                {
                    return false;
                }

                var options = await userAssignmentsQueryService.ListDgiOrCountryAssignmentsAsync(userId, cancellationToken);
                return ownerId is { } chosen
                    ? options.Any(o => o.OrgNodeId == chosen)
                    : options.Count == 1;
            })
            .WithMessage("Choose which organisation this lens set belongs to.");
    }
}

/// <summary>Shared by the create and update validators — one sentence for one rule.</summary>
public static class LensSetNameMessages
{
    public const string Taken = "A lens set with this name already exists. Choose another name.";
    public const string Missing = "Enter a name for the lens set.";
    public const string TooLong = "Keep the name to 200 characters or fewer.";
    public const string DescriptionTooLong = "Keep the description to 500 characters or fewer.";
}

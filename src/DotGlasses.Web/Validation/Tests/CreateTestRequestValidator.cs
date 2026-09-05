using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.Tests;
using DotGlasses.Rules;
using FluentValidation;
using ContractLensRangeType = DotGlasses.Contracts.Common.LensRangeType;
using ReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.Tests;

/// <summary>
/// Lives in Web, not co-located with CreateTestRequest in Contracts like WidgetExample's
/// validator was — this one needs IReferenceDataLookupService (Application), and Contracts must
/// never depend on Application (DotGlasses.App only references Contracts; see CLAUDE.md's
/// Architecture rules).
/// </summary>
public class CreateTestRequestValidator : AbstractValidator<CreateTestRequest>
{
    public CreateTestRequestValidator(IReferenceDataLookupService referenceData, IReferenceDataSnapshotProvider snapshots)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.Outcome).IsInEnum();
        RuleFor(x => x.AgeYears).InclusiveBetween(0, 120).When(x => x.AgeYears.HasValue);
        RuleFor(x => x.OccupationOtherText).MaximumLength(200);
        RuleFor(x => x.ReferralOtherText).MaximumLength(200);
        RuleFor(x => x.ReferralLocationFreeText).MaximumLength(500);
        RuleFor(x => x.LensTypeOtherText).MaximumLength(200);

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            // Occupation and "referred or treated" (ticket 09) and now the whole lens range
            // (ticket 10) live in DotGlasses.Rules, which the Field App checks against too; only
            // the Coating preference stays here, until ticket 11.
            //
            // The lens-option availability check below used to sit *inside* the preset branch,
            // between "LensOptionRightId must belong to PresetCatalogueId" and the pupil-distance
            // checks; it now runs after every lens-range failure instead. Nothing a caller can act
            // on changes: its key (CoatingPreferenceRefId) is disjoint from every lens-range key,
            // so no message moves between fields, and both the Field App's error bag and
            // ValidationProblemDetails are keyed by field rather than ordered. What does still
            // matter is its order against the active-item check that follows it — both report
            // against CoatingPreferenceRefId, so a request that fails both must keep seeing
            // availability first, as it did before.
            var snapshot = await snapshots.GetAsync(cancellationToken);
            foreach (var failure in ConsultationRules.Check(request, snapshot).Failures)
            {
                context.AddFailure(failure.Key, failure.Message);
            }

            await ValidateCoatingAvailabilityForPresetLensAsync(request, context, referenceData, cancellationToken);
            await ValidateCoatingPreferenceAsync(request, context, referenceData, cancellationToken);
        });
    }

    /// <summary>
    /// Ticket 11's remit, left in place deliberately: availability is scoped by the chosen lens
    /// option, so this rule only exists inside the preset branch and only once that branch has all
    /// three ids — the same short-circuit ConsultationRules.PresetBranch applies before it says
    /// anything about the lens options themselves. Re-deriving the branch here is the price of
    /// splitting the topic across two tickets; ticket 11 folds it into the shared module and the
    /// duplication goes away with it.
    /// </summary>
    private static async Task ValidateCoatingAvailabilityForPresetLensAsync(
        CreateTestRequest request, ValidationContext<CreateTestRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        if (request.LensRangeType is not (ContractLensRangeType.SixLensSet or ContractLensRangeType.NineLensSet)
            || request.PresetCatalogueId is null || request.LensOptionRightId is null
            || request.LensOptionLeftId is not { } leftId
            || request.CoatingPreferenceRefId is not { } coatingPreferenceRefId)
        {
            return;
        }

        if (!await referenceData.IsCoatingAvailableForLensOptionAsync(leftId, coatingPreferenceRefId, cancellationToken))
        {
            context.AddFailure(nameof(request.CoatingPreferenceRefId), "CoatingPreferenceRefId is not configured as available for the chosen lens option (see Reference Data > Lens Strength).");
        }
    }

    /// <summary>Ticket 11's remit. Unlike the availability check above this one is asked for every
    /// LensRangeType, the unset one included — a Coating preference can be recorded before any
    /// lens has been chosen.</summary>
    private static async Task ValidateCoatingPreferenceAsync(
        CreateTestRequest request, ValidationContext<CreateTestRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        if (request.CoatingPreferenceRefId is not { } coatingRefId)
        {
            return;
        }

        var lookup = await referenceData.LookupAsync(coatingRefId, ReferenceDataCategory.Coating, cancellationToken);
        if (lookup is not { IsActive: true })
        {
            context.AddFailure(nameof(request.CoatingPreferenceRefId), "CoatingPreferenceRefId must reference an existing, active Coating reference-data item.");
        }
    }
}

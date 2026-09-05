using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.VisionTests;
using DotGlasses.Contracts.Leads;
using DotGlasses.Rules;
using FluentValidation;
using ContractLensRangeType = DotGlasses.Contracts.Common.LensRangeType;
using ReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.Leads;

/// <summary>Lives in Web, not Contracts — see CreateTestRequestValidator's doc comment for why.</summary>
public class CreateLeadRequestValidator : AbstractValidator<CreateLeadRequest>
{
    public CreateLeadRequestValidator(IReferenceDataLookupService referenceData, IReferenceDataSnapshotProvider snapshots, IVisionTestRepository testRepository)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.AgeYears).InclusiveBetween(0, 120).When(x => x.AgeYears.HasValue);
        RuleFor(x => x.OccupationOtherText).MaximumLength(200);
        RuleFor(x => x.ReasonNotPurchasedOtherText).MaximumLength(200);
        RuleFor(x => x.ReferralOtherText).MaximumLength(200);
        RuleFor(x => x.ReferralLocationFreeText).MaximumLength(500);

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            // Occupation, "referred or treated" and reason-not-purchased (ticket 09) and now the
            // whole lens range (ticket 10) live in DotGlasses.Rules, which the Field App checks
            // against too; only the Coating preference stays here, until ticket 11.
            //
            // The lens-range failures are now raised ahead of the Coating ones rather than between
            // them, and the lens-option availability check has moved out of the preset branch to
            // sit after the active-item check it used to precede in the code but follow in
            // execution. Neither changes anything a caller can act on: every lens-range key is
            // disjoint from CoatingPreferenceRefId, and both the Field App's error bag and
            // ValidationProblemDetails are keyed by field rather than ordered. The order that does
            // still matter is between the two Coating checks themselves — both report against
            // CoatingPreferenceRefId, and on a Lead the active-item check has always come first
            // (note this is the opposite way round from a Test, which is pre-existing and
            // preserved).
            var snapshot = await snapshots.GetAsync(cancellationToken);
            foreach (var failure in ConsultationRules.Check(request, snapshot).Failures)
            {
                context.AddFailure(failure.Key, failure.Message);
            }

            await ValidateCoatingPreferenceAsync(request, context, referenceData, cancellationToken);
            await ValidateCoatingAvailabilityForPresetLensAsync(request, context, referenceData, cancellationToken);
            await ValidateSourceTestAsync(request, context, testRepository, cancellationToken);
        });
    }

    /// <summary>Ticket 11's remit. Asked for every LensRangeType, the unset one included — a
    /// Coating preference can be recorded before any lens has been chosen.</summary>
    private static async Task ValidateCoatingPreferenceAsync(
        CreateLeadRequest request, ValidationContext<CreateLeadRequest> context,
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

    /// <summary>
    /// Ticket 11's remit, left in place deliberately: availability is scoped by the chosen lens
    /// option, so this rule only exists inside the preset branch and only once that branch has all
    /// three ids — the same short-circuit ConsultationRules.PresetBranch applies before it says
    /// anything about the lens options themselves. Re-deriving the branch here is the price of
    /// splitting the topic across two tickets; ticket 11 folds it into the shared module and the
    /// duplication goes away with it.
    /// </summary>
    private static async Task ValidateCoatingAvailabilityForPresetLensAsync(
        CreateLeadRequest request, ValidationContext<CreateLeadRequest> context,
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

    private static async Task ValidateSourceTestAsync(
        CreateLeadRequest request, ValidationContext<CreateLeadRequest> context,
        IVisionTestRepository testRepository, CancellationToken cancellationToken)
    {
        if (request.SourceTestId is not { } sourceTestId)
        {
            return;
        }

        var test = await testRepository.GetByIdAsync(sourceTestId, cancellationToken);
        if (test is null)
        {
            context.AddFailure(nameof(request.SourceTestId), "SourceTestId must reference an existing Test.");
        }
        else if (test.ConvertedToLeadId is not null)
        {
            context.AddFailure(nameof(request.SourceTestId), "This Test has already been converted into a Lead.");
        }
    }
}

using DotGlasses.Application.Leads;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.Sales;
using DotGlasses.Rules;
using FluentValidation;
using ContractLensRangeType = DotGlasses.Contracts.Common.LensRangeType;

namespace DotGlasses.Web.Validation.Sales;

/// <summary>Lives in Web, not Contracts — see CreateTestRequestValidator's doc comment for why.</summary>
public class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator(IReferenceDataSnapshotProvider snapshots, ILeadRepository leadRepository)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).MaximumLength(32);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.AgeYears).InclusiveBetween(0, 120).When(x => x.AgeYears.HasValue);
        RuleFor(x => x.LensRangeType).IsInEnum();
        RuleFor(x => x.FrameCoverage).IsInEnum();
        RuleFor(x => x.OccupationOtherText).MaximumLength(200);
        RuleFor(x => x.FrameColourOtherText).MaximumLength(200);
        RuleFor(x => x.HardCaseOtherColourText).MaximumLength(200);
        RuleFor(x => x.ReferralOtherText).MaximumLength(200);
        RuleFor(x => x.ReferralLocationFreeText).MaximumLength(500);
        RuleFor(x => x.OrderFromDotGlasses)
            .Equal(false)
            .When(x => x.LensRangeType != ContractLensRangeType.Custom)
            .WithMessage("OrderFromDotGlasses is only meaningful when LensRangeType is Custom.");

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            // Every consultation rule the reference-data snapshot can answer now lives in
            // DotGlasses.Rules, which the Field App checks against too — ticket 11 moved the last
            // of them, the Coating set. What is left here is the scalar RuleFor chain above and
            // the source-Lead check below, which reads a hierarchy-scoped row through a repository
            // and so can never live in the shared module.
            var snapshot = await snapshots.GetAsync(cancellationToken);
            foreach (var failure in ConsultationRules.Check(request, snapshot).Failures)
            {
                context.AddFailure(failure.Key, failure.Message);
            }

            await ValidateSourceLeadAsync(request, context, leadRepository, cancellationToken);
        });
    }

    private static async Task ValidateSourceLeadAsync(
        CreateSaleRequest request, ValidationContext<CreateSaleRequest> context,
        ILeadRepository leadRepository, CancellationToken cancellationToken)
    {
        if (request.SourceLeadId is not { } sourceLeadId)
        {
            return;
        }

        var lead = await leadRepository.GetByIdAsync(sourceLeadId, cancellationToken);
        if (lead is null)
        {
            context.AddFailure(nameof(request.SourceLeadId), "SourceLeadId must reference an existing Lead.");
        }
        else if (lead.SaleId is not null)
        {
            context.AddFailure(nameof(request.SourceLeadId), "This Lead has already been converted into a Sale.");
        }
    }
}

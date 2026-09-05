using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.VisionTests;
using DotGlasses.Contracts.Leads;
using DotGlasses.Rules;
using FluentValidation;

namespace DotGlasses.Web.Validation.Leads;

/// <summary>Lives in Web, not Contracts — see CreateTestRequestValidator's doc comment for why.</summary>
public class CreateLeadRequestValidator : AbstractValidator<CreateLeadRequest>
{
    public CreateLeadRequestValidator(IReferenceDataSnapshotProvider snapshots, IVisionTestRepository testRepository)
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
            // Every consultation rule the reference-data snapshot can answer now lives in
            // DotGlasses.Rules, which the Field App checks against too — ticket 11 moved the last
            // of them, the Coating preference. What is left here is the scalar RuleFor chain above
            // and the source-Test check below, which reads a hierarchy-scoped row through a
            // repository and so can never live in the shared module.
            var snapshot = await snapshots.GetAsync(cancellationToken);
            foreach (var failure in ConsultationRules.Check(request, snapshot).Failures)
            {
                context.AddFailure(failure.Key, failure.Message);
            }

            await ValidateSourceTestAsync(request, context, testRepository, cancellationToken);
        });
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

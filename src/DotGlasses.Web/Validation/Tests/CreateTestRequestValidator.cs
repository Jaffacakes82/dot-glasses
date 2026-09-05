using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.Tests;
using DotGlasses.Rules;
using FluentValidation;

namespace DotGlasses.Web.Validation.Tests;

/// <summary>
/// Lives in Web, not co-located with CreateTestRequest in Contracts like WidgetExample's
/// validator was — this one needs IReferenceDataSnapshotProvider (Application), and Contracts must
/// never depend on Application (DotGlasses.App only references Contracts; see CLAUDE.md's
/// Architecture rules).
/// </summary>
public class CreateTestRequestValidator : AbstractValidator<CreateTestRequest>
{
    public CreateTestRequestValidator(IReferenceDataSnapshotProvider snapshots)
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
            // Every consultation rule the reference-data snapshot can answer now lives in
            // DotGlasses.Rules, which the Field App checks against too — ticket 11 moved the last
            // of them, the Coating preference. What is left here is the scalar RuleFor chain
            // above, which the snapshot has no opinion on.
            var snapshot = await snapshots.GetAsync(cancellationToken);
            foreach (var failure in ConsultationRules.Check(request, snapshot).Failures)
            {
                context.AddFailure(failure.Key, failure.Message);
            }
        });
    }
}

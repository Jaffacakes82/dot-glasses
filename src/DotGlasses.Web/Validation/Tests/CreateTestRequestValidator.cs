using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.Tests;
using FluentValidation;
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
    public CreateTestRequestValidator(IReferenceDataLookupService referenceData)
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.Outcome).IsInEnum();
        RuleFor(x => x.AgeYears).InclusiveBetween(0, 120).When(x => x.AgeYears.HasValue);
        RuleFor(x => x.OccupationOtherText).MaximumLength(200);
        RuleFor(x => x.ReferralOtherText).MaximumLength(200);
        RuleFor(x => x.ReferralLocationFreeText).MaximumLength(500);

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            await ValidateOccupationAsync(request, context, referenceData, cancellationToken);
            await ValidateReferralAsync(request, context, referenceData, cancellationToken);
        });
    }

    private static async Task ValidateOccupationAsync(
        CreateTestRequest request, ValidationContext<CreateTestRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        if (request.OccupationRefId is not { } occupationRefId)
        {
            return;
        }

        var lookup = await referenceData.LookupAsync(occupationRefId, ReferenceDataCategory.Occupation, cancellationToken);
        if (lookup is not { IsActive: true })
        {
            context.AddFailure(nameof(request.OccupationRefId), "OccupationRefId must reference an existing, active Occupation reference-data item.");
            return;
        }

        if (lookup.IsOtherOption && string.IsNullOrWhiteSpace(request.OccupationOtherText))
        {
            context.AddFailure(nameof(request.OccupationOtherText), "OccupationOtherText is required when Occupation is \"Other\".");
        }
    }

    /// <summary>"Referred or treated" is orthogonal to Outcome (2026-09-03) — not gated on any
    /// particular outcome/result. Mirrored near-identically in CreateLeadRequestValidator/
    /// CreateSaleRequestValidator, since Test/Lead/Sale share the exact same five-field shape.</summary>
    private static async Task ValidateReferralAsync(
        CreateTestRequest request, ValidationContext<CreateTestRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        if (!request.ReferredOrTreated)
        {
            if (request.ReferralReasonRefId is not null || request.ReferralOtherText is not null
                || request.ReferralLocationFreeText is not null || request.TreatedInFacility)
            {
                context.AddFailure(nameof(request.ReferredOrTreated), "Referral/treatment fields must be empty unless ReferredOrTreated is true.");
            }

            return;
        }

        if (request.ReferralReasonRefId is not { } referralReasonRefId)
        {
            context.AddFailure(nameof(request.ReferralReasonRefId), "ReferralReasonRefId is required when ReferredOrTreated is true.");
        }
        else
        {
            var lookup = await referenceData.LookupAsync(referralReasonRefId, ReferenceDataCategory.ReferralReason, cancellationToken);
            if (lookup is not { IsActive: true })
            {
                context.AddFailure(nameof(request.ReferralReasonRefId), "ReferralReasonRefId must reference an existing, active ReferralReason reference-data item.");
            }
            else if (lookup.IsOtherOption && string.IsNullOrWhiteSpace(request.ReferralOtherText))
            {
                context.AddFailure(nameof(request.ReferralOtherText), "ReferralOtherText is required when ReferralReason is \"Other\".");
            }
        }

        // Treated in-house has no external location to name — the reason stays required either
        // way, only the location requirement flips.
        if (request.TreatedInFacility)
        {
            if (!string.IsNullOrWhiteSpace(request.ReferralLocationFreeText))
            {
                context.AddFailure(nameof(request.ReferralLocationFreeText), "ReferralLocationFreeText must be empty when TreatedInFacility is true.");
            }
        }
        else if (string.IsNullOrWhiteSpace(request.ReferralLocationFreeText))
        {
            context.AddFailure(nameof(request.ReferralLocationFreeText), "ReferralLocationFreeText is required when ReferredOrTreated is true and TreatedInFacility is false.");
        }
    }
}

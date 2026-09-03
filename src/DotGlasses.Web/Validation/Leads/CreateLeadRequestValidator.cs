using DotGlasses.Application.ReferenceData;
using DotGlasses.Application.VisionTests;
using DotGlasses.Contracts.Leads;
using FluentValidation;
using ContractLensRangeType = DotGlasses.Contracts.Common.LensRangeType;
using ReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.Leads;

/// <summary>Lives in Web, not Contracts — see CreateTestRequestValidator's doc comment for why.</summary>
public class CreateLeadRequestValidator : AbstractValidator<CreateLeadRequest>
{
    public CreateLeadRequestValidator(IReferenceDataLookupService referenceData, IVisionTestRepository testRepository)
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
            await ValidateOccupationAsync(request, context, referenceData, cancellationToken);
            await ValidateReferralAsync(request, context, referenceData, cancellationToken);
            await ValidateReasonNotPurchasedAsync(request, context, referenceData, cancellationToken);
            await ValidateCoatingPreferenceAsync(request, context, referenceData, cancellationToken);
            await ValidateLensRangeAsync(request, context, referenceData, cancellationToken);
            await ValidateSourceTestAsync(request, context, testRepository, cancellationToken);
        });
    }

    private static async Task ValidateOccupationAsync(
        CreateLeadRequest request, ValidationContext<CreateLeadRequest> context,
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

    /// <summary>"Referred or treated" — mirrors CreateTestRequestValidator's ValidateReferralAsync
    /// exactly (see that file's doc comment); Test/Lead/Sale share the same five-field shape.</summary>
    private static async Task ValidateReferralAsync(
        CreateLeadRequest request, ValidationContext<CreateLeadRequest> context,
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

    private static async Task ValidateReasonNotPurchasedAsync(
        CreateLeadRequest request, ValidationContext<CreateLeadRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        var lookup = await referenceData.LookupAsync(request.ReasonNotPurchasedRefId, ReferenceDataCategory.ReasonNotPurchased, cancellationToken);
        if (lookup is not { IsActive: true })
        {
            context.AddFailure(nameof(request.ReasonNotPurchasedRefId), "ReasonNotPurchasedRefId must reference an existing, active ReasonNotPurchased reference-data item.");
            return;
        }

        if (lookup.IsOtherOption && string.IsNullOrWhiteSpace(request.ReasonNotPurchasedOtherText))
        {
            context.AddFailure(nameof(request.ReasonNotPurchasedOtherText), "ReasonNotPurchasedOtherText is required when ReasonNotPurchased is \"Other\".");
        }
    }

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

    private static async Task ValidateLensRangeAsync(
        CreateLeadRequest request, ValidationContext<CreateLeadRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        var presetFieldsSet = request.PresetCatalogueId is not null || request.LensOptionLeftId is not null || request.LensOptionRightId is not null;
        var customFieldsSet = request.CustomSphereLeft is not null || request.CustomCylinderLeft is not null || request.CustomAxisLeft is not null || request.CustomAddPowerLeft is not null
            || request.CustomSphereRight is not null || request.CustomCylinderRight is not null || request.CustomAxisRight is not null || request.CustomAddPowerRight is not null;

        switch (request.LensRangeType)
        {
            case null:
                if (presetFieldsSet || customFieldsSet || request.PupilDistanceMm is not null || request.PresetPupilDistanceBucket is not null)
                {
                    context.AddFailure(nameof(request.LensRangeType), "Preset/custom lens fields must be empty when LensRangeType is not set.");
                }

                break;

            case ContractLensRangeType.SixLensSet or ContractLensRangeType.NineLensSet:
                if (customFieldsSet)
                {
                    context.AddFailure(nameof(request.LensRangeType), "Custom prescription fields must be empty for a preset LensRangeType.");
                }

                if (request.PresetCatalogueId is not { } catalogueId || request.LensOptionLeftId is not { } leftId || request.LensOptionRightId is not { } rightId)
                {
                    context.AddFailure(nameof(request.PresetCatalogueId), "PresetCatalogueId, LensOptionLeftId and LensOptionRightId are all required for a preset LensRangeType.");
                    break;
                }

                if (!await referenceData.LensOptionBelongsToCatalogueAsync(leftId, catalogueId, cancellationToken))
                {
                    context.AddFailure(nameof(request.LensOptionLeftId), "LensOptionLeftId must belong to PresetCatalogueId.");
                }

                if (!await referenceData.LensOptionBelongsToCatalogueAsync(rightId, catalogueId, cancellationToken))
                {
                    context.AddFailure(nameof(request.LensOptionRightId), "LensOptionRightId must belong to PresetCatalogueId.");
                }

                if (request.CoatingPreferenceRefId is { } coatingPreferenceRefId
                    && !await referenceData.IsCoatingAvailableForLensOptionAsync(leftId, coatingPreferenceRefId, cancellationToken))
                {
                    context.AddFailure(nameof(request.CoatingPreferenceRefId), "CoatingPreferenceRefId is not configured as available for the chosen lens option (see Reference Data > Lens Strength).");
                }

                if (request.PupilDistanceMm is not null)
                {
                    context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm must be empty for a preset LensRangeType — use PresetPupilDistanceBucket instead.");
                }

                var maxPdBucket = request.ChildrensFrame ? 2 : 4;
                if (request.PresetPupilDistanceBucket is { } pdBucket && (pdBucket < 0 || pdBucket > maxPdBucket))
                {
                    context.AddFailure(nameof(request.PresetPupilDistanceBucket), $"PresetPupilDistanceBucket must be between 0 and {maxPdBucket} for a preset LensRangeType{(request.ChildrensFrame ? " (0-2 for a children's frame)" : "")}.");
                }

                break;

            case ContractLensRangeType.Custom:
                if (presetFieldsSet)
                {
                    context.AddFailure(nameof(request.LensRangeType), "Preset fields must be empty for a Custom LensRangeType.");
                }

                if (request.CustomSphereLeft is null || request.CustomSphereRight is null)
                {
                    context.AddFailure(nameof(request.LensRangeType), "CustomSphereLeft and CustomSphereRight are required for a Custom LensRangeType.");
                }

                ValidateCustomPower(request.CustomSphereLeft, nameof(request.CustomSphereLeft), -10m, 10m, 0.25m, context);
                ValidateCustomPower(request.CustomSphereRight, nameof(request.CustomSphereRight), -10m, 10m, 0.25m, context);
                ValidateCustomPower(request.CustomCylinderLeft, nameof(request.CustomCylinderLeft), -10m, 10m, 0.25m, context);
                ValidateCustomPower(request.CustomCylinderRight, nameof(request.CustomCylinderRight), -10m, 10m, 0.25m, context);
                ValidateCustomPower(request.CustomAddPowerLeft, nameof(request.CustomAddPowerLeft), 0m, 3m, 0.25m, context);
                ValidateCustomPower(request.CustomAddPowerRight, nameof(request.CustomAddPowerRight), 0m, 3m, 0.25m, context);
                ValidateCustomAxis(request.CustomAxisLeft, nameof(request.CustomAxisLeft), context);
                ValidateCustomAxis(request.CustomAxisRight, nameof(request.CustomAxisRight), context);

                if (request.PresetPupilDistanceBucket is not null)
                {
                    context.AddFailure(nameof(request.PresetPupilDistanceBucket), "PresetPupilDistanceBucket must be empty for a Custom LensRangeType — use PupilDistanceMm instead.");
                }

                if (request.PupilDistanceMm is not { } pdCustom || pdCustom < 54 || pdCustom > 74)
                {
                    context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm is required and must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).");
                }
                else if (pdCustom != Math.Truncate(pdCustom))
                {
                    context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm must be a whole millimetre value.");
                }

                break;
        }
    }

    /// <summary>Sphere/Cylinder/Add-power are physical lens-grinding constraints, not admin-curated
    /// reference data — validated in code against the user's exact spec, not a lookup table.</summary>
    private static void ValidateCustomPower(decimal? value, string propertyName, decimal min, decimal max, decimal step, ValidationContext<CreateLeadRequest> context)
    {
        if (value is not { } v)
        {
            return;
        }

        if (v < min || v > max || (v - min) % step != 0)
        {
            context.AddFailure(propertyName, $"{propertyName} must be between {min} and {max} in {step} increments.");
        }
    }

    private static void ValidateCustomAxis(decimal? value, string propertyName, ValidationContext<CreateLeadRequest> context)
    {
        if (value is not { } v)
        {
            return;
        }

        if (v < 0 || v > 180 || v != Math.Truncate(v))
        {
            context.AddFailure(propertyName, $"{propertyName} must be a whole number of degrees between 0 and 180.");
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

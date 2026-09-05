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
            // Occupation and "referred or treated" now live in DotGlasses.Rules (ticket 09), which
            // the Field App checks against too; lens range stays here until ticket 10.
            var snapshot = await snapshots.GetAsync(cancellationToken);
            foreach (var failure in ConsultationRules.Check(request, snapshot).Failures)
            {
                context.AddFailure(failure.Key, failure.Message);
            }

            await ValidateLensRangeAsync(request, context, referenceData, cancellationToken);
        });
    }

    /// <summary>Which lens(es) are needed — recordable whenever Outcome == NeedsGlasses, with no
    /// preference required (a Test may record the outcome alone, same reasoning as Lead's
    /// equivalent block). Mirrors CreateLeadRequestValidator's ValidateLensRangeAsync exactly —
    /// Test's lens shape is identical to Lead's, both optional throughout.</summary>
    private static async Task ValidateLensRangeAsync(
        CreateTestRequest request, ValidationContext<CreateTestRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        var presetFieldsSet = request.PresetCatalogueId is not null || request.LensOptionLeftId is not null || request.LensOptionRightId is not null;
        var customFieldsSet = request.CustomSphereLeft is not null || request.CustomCylinderLeft is not null || request.CustomAxisLeft is not null || request.CustomAddPowerLeft is not null
            || request.CustomSphereRight is not null || request.CustomCylinderRight is not null || request.CustomAxisRight is not null || request.CustomAddPowerRight is not null
            || request.LensTypeRefId is not null || request.LensTypeOtherText is not null;

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
                    context.AddFailure(nameof(request.PresetPupilDistanceBucket), $"PresetPupilDistanceBucket must be between 0 and {maxPdBucket}.");
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
                await ValidateLensTypeAsync(request, context, referenceData, cancellationToken);

                if (request.PresetPupilDistanceBucket is not null)
                {
                    context.AddFailure(nameof(request.PresetPupilDistanceBucket), "PresetPupilDistanceBucket must be empty for a Custom LensRangeType — use PupilDistanceMm instead.");
                }

                // Optional here — same "no time to measure" reasoning as Lead.
                if (request.PupilDistanceMm is { } pdCustom)
                {
                    if (pdCustom < 54 || pdCustom > 74)
                    {
                        context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).");
                    }
                    else if (pdCustom != Math.Truncate(pdCustom))
                    {
                        context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm must be a whole millimetre value.");
                    }
                }

                break;
        }

        if (request.CoatingPreferenceRefId is { } coatingRefId)
        {
            var lookup = await referenceData.LookupAsync(coatingRefId, ReferenceDataCategory.Coating, cancellationToken);
            if (lookup is not { IsActive: true })
            {
                context.AddFailure(nameof(request.CoatingPreferenceRefId), "CoatingPreferenceRefId must reference an existing, active Coating reference-data item.");
            }
        }
    }

    /// <summary>Asked only once either eye carries two distinct powers (a base sphere plus an add
    /// power) — required in that case, must stay empty otherwise.</summary>
    private static async Task ValidateLensTypeAsync(
        CreateTestRequest request, ValidationContext<CreateTestRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        var hasTwoPowers = request.CustomAddPowerLeft is not null || request.CustomAddPowerRight is not null;
        if (!hasTwoPowers)
        {
            if (request.LensTypeRefId is not null || request.LensTypeOtherText is not null)
            {
                context.AddFailure(nameof(request.LensTypeRefId), "LensTypeRefId/LensTypeOtherText must be empty unless an add power is set.");
            }

            return;
        }

        if (request.LensTypeRefId is not { } lensTypeRefId)
        {
            context.AddFailure(nameof(request.LensTypeRefId), "LensTypeRefId is required when an add power is set (two distinct powers on that eye).");
            return;
        }

        var lookup = await referenceData.LookupAsync(lensTypeRefId, ReferenceDataCategory.LensType, cancellationToken);
        if (lookup is not { IsActive: true })
        {
            context.AddFailure(nameof(request.LensTypeRefId), "LensTypeRefId must reference an existing, active LensType reference-data item.");
        }
        else if (lookup.IsOtherOption && string.IsNullOrWhiteSpace(request.LensTypeOtherText))
        {
            context.AddFailure(nameof(request.LensTypeOtherText), "LensTypeOtherText is required when LensType is \"Other\".");
        }
    }

    /// <summary>Sphere/Cylinder/Add-power are physical lens-grinding constraints, not admin-curated
    /// reference data — validated in code against the user's exact spec, not a lookup table.</summary>
    private static void ValidateCustomPower(decimal? value, string propertyName, decimal min, decimal max, decimal step, ValidationContext<CreateTestRequest> context)
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

    private static void ValidateCustomAxis(decimal? value, string propertyName, ValidationContext<CreateTestRequest> context)
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
}

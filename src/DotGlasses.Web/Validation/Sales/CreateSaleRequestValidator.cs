using DotGlasses.Application.Leads;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.Sales;
using FluentValidation;
using ContractLensRangeType = DotGlasses.Contracts.Common.LensRangeType;
using ReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.Sales;

/// <summary>Lives in Web, not Contracts — see CreateTestRequestValidator's doc comment for why.</summary>
public class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator(IReferenceDataLookupService referenceData, ILeadRepository leadRepository)
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
        RuleFor(x => x.OrderFromDotGlasses)
            .Equal(false)
            .When(x => x.LensRangeType != ContractLensRangeType.Custom)
            .WithMessage("OrderFromDotGlasses is only meaningful when LensRangeType is Custom.");

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            await ValidateOccupationAsync(request, context, referenceData, cancellationToken);
            await ValidateLensRangeAsync(request, context, referenceData, cancellationToken);
            await ValidateFrameColourAsync(request, context, referenceData, cancellationToken);
            await ValidateHardCaseAsync(request, context, referenceData, cancellationToken);
            await ValidateSourceLeadAsync(request, context, leadRepository, cancellationToken);
        });
    }

    private static async Task ValidateOccupationAsync(
        CreateSaleRequest request, ValidationContext<CreateSaleRequest> context,
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

    private static async Task ValidateLensRangeAsync(
        CreateSaleRequest request, ValidationContext<CreateSaleRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        var customFieldsSet = request.CustomSphereLeft is not null || request.CustomCylinderLeft is not null || request.CustomAxisLeft is not null || request.CustomAddPowerLeft is not null
            || request.CustomSphereRight is not null || request.CustomCylinderRight is not null || request.CustomAxisRight is not null || request.CustomAddPowerRight is not null;
        var presetFieldsSet = request.PresetCatalogueId is not null || request.LensOptionLeftId is not null || request.LensOptionRightId is not null;

        if (request.LensRangeType is ContractLensRangeType.SixLensSet or ContractLensRangeType.NineLensSet)
        {
            if (customFieldsSet)
            {
                context.AddFailure(nameof(request.LensRangeType), "Custom prescription fields must be empty for a preset LensRangeType.");
            }

            if (request.PresetCatalogueId is not { } catalogueId || request.LensOptionLeftId is not { } leftId || request.LensOptionRightId is not { } rightId)
            {
                context.AddFailure(nameof(request.PresetCatalogueId), "PresetCatalogueId, LensOptionLeftId and LensOptionRightId are all required for a preset LensRangeType.");
                return;
            }

            if (!await referenceData.LensOptionBelongsToCatalogueAsync(leftId, catalogueId, cancellationToken))
            {
                context.AddFailure(nameof(request.LensOptionLeftId), "LensOptionLeftId must belong to PresetCatalogueId.");
            }

            if (!await referenceData.LensOptionBelongsToCatalogueAsync(rightId, catalogueId, cancellationToken))
            {
                context.AddFailure(nameof(request.LensOptionRightId), "LensOptionRightId must belong to PresetCatalogueId.");
            }

            if (request.PupilDistanceMm is not null)
            {
                context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm must be empty for a preset LensRangeType — use PresetPupilDistanceBucket instead.");
            }

            var maxPdBucket = request.ChildrensFrame ? 2 : 4;
            if (request.PresetPupilDistanceBucket is not { } pdBucket || pdBucket < 0 || pdBucket > maxPdBucket)
            {
                context.AddFailure(nameof(request.PresetPupilDistanceBucket), $"PresetPupilDistanceBucket is required and must be between 0 and {maxPdBucket} for a preset LensRangeType{(request.ChildrensFrame ? " (0-2 for a children's frame)" : "")}.");
            }

            if (request.CoatingRefId is not { } presetCoatingRefId)
            {
                context.AddFailure(nameof(request.CoatingRefId), "CoatingRefId is required for a preset LensRangeType.");
            }
            else
            {
                var presetCoatingLookup = await referenceData.LookupAsync(presetCoatingRefId, ReferenceDataCategory.Coating, cancellationToken);
                if (presetCoatingLookup is not { IsActive: true })
                {
                    context.AddFailure(nameof(request.CoatingRefId), "CoatingRefId must reference an existing, active Coating reference-data item.");
                }
                else if (!await referenceData.IsCoatingAvailableForLensOptionAsync(leftId, presetCoatingRefId, cancellationToken))
                {
                    context.AddFailure(nameof(request.CoatingRefId), "CoatingRefId is not configured as available for the chosen lens option (see Reference Data > Lens Strength).");
                }
            }

            return;
        }

        if (request.LensRangeType == ContractLensRangeType.Custom)
        {
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
            ValidateCustomPower(request.CustomCylinderLeft, nameof(request.CustomCylinderLeft), -6m, 0.25m, 0.25m, context);
            ValidateCustomPower(request.CustomCylinderRight, nameof(request.CustomCylinderRight), -6m, 0.25m, 0.25m, context);
            ValidateCustomPower(request.CustomAddPowerLeft, nameof(request.CustomAddPowerLeft), 0m, 3m, 0.25m, context);
            ValidateCustomPower(request.CustomAddPowerRight, nameof(request.CustomAddPowerRight), 0m, 3m, 0.25m, context);
            ValidateCustomAxis(request.CustomAxisLeft, nameof(request.CustomAxisLeft), context);
            ValidateCustomAxis(request.CustomAxisRight, nameof(request.CustomAxisRight), context);

            if (request.PresetPupilDistanceBucket is not null)
            {
                context.AddFailure(nameof(request.PresetPupilDistanceBucket), "PresetPupilDistanceBucket must be empty for a Custom LensRangeType — use PupilDistanceMm instead.");
            }

            if (request.PupilDistanceMm is not { } pd || pd < 54 || pd > 74)
            {
                context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm is required and must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).");
            }
            else if (pd != Math.Truncate(pd))
            {
                context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm must be a whole millimetre value.");
            }

            if (request.CoatingRefId is not { } coatingRefId)
            {
                context.AddFailure(nameof(request.CoatingRefId), "CoatingRefId is required for a Custom LensRangeType.");
            }
            else
            {
                var coatingLookup = await referenceData.LookupAsync(coatingRefId, ReferenceDataCategory.Coating, cancellationToken);
                if (coatingLookup is not { IsActive: true })
                {
                    context.AddFailure(nameof(request.CoatingRefId), "CoatingRefId must reference an existing, active Coating reference-data item.");
                }
            }
        }
    }

    /// <summary>Sphere/Cylinder/Add-power are physical lens-grinding constraints, not admin-curated
    /// reference data — validated in code against the user's exact spec, not a lookup table.</summary>
    private static void ValidateCustomPower(decimal? value, string propertyName, decimal min, decimal max, decimal step, ValidationContext<CreateSaleRequest> context)
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

    private static void ValidateCustomAxis(decimal? value, string propertyName, ValidationContext<CreateSaleRequest> context)
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

    private static async Task ValidateFrameColourAsync(
        CreateSaleRequest request, ValidationContext<CreateSaleRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        var lookup = await referenceData.LookupAsync(request.FrameColourRefId, ReferenceDataCategory.FrameColour, cancellationToken);
        if (lookup is not { IsActive: true })
        {
            context.AddFailure(nameof(request.FrameColourRefId), "FrameColourRefId must reference an existing, active FrameColour reference-data item.");
            return;
        }

        if (lookup.IsOtherOption && string.IsNullOrWhiteSpace(request.FrameColourOtherText))
        {
            context.AddFailure(nameof(request.FrameColourOtherText), "FrameColourOtherText is required when FrameColour is \"Other\".");
        }
    }

    private static async Task ValidateHardCaseAsync(
        CreateSaleRequest request, ValidationContext<CreateSaleRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        if (!request.HardCaseSold)
        {
            if (request.HardCaseColourRefId is not null || request.HardCaseOtherColourText is not null)
            {
                context.AddFailure(nameof(request.HardCaseSold), "HardCaseColourRefId/HardCaseOtherColourText must be empty when HardCaseSold is false.");
            }

            return;
        }

        if (request.HardCaseColourRefId is not { } hardCaseColourRefId)
        {
            context.AddFailure(nameof(request.HardCaseColourRefId), "HardCaseColourRefId is required when HardCaseSold is true.");
            return;
        }

        var lookup = await referenceData.LookupAsync(hardCaseColourRefId, ReferenceDataCategory.HardCaseColour, cancellationToken);
        if (lookup is not { IsActive: true })
        {
            context.AddFailure(nameof(request.HardCaseColourRefId), "HardCaseColourRefId must reference an existing, active HardCaseColour reference-data item.");
            return;
        }

        if (lookup.IsOtherOption && string.IsNullOrWhiteSpace(request.HardCaseOtherColourText))
        {
            context.AddFailure(nameof(request.HardCaseOtherColourText), "HardCaseOtherColourText is required when HardCaseColour is \"Other\".");
        }
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

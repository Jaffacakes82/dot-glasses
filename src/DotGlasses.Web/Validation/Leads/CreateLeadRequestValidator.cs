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

        RuleFor(x => x).CustomAsync(async (request, context, cancellationToken) =>
        {
            await ValidateOccupationAsync(request, context, referenceData, cancellationToken);
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
                if (presetFieldsSet || customFieldsSet)
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

                if (request.PupilDistanceMm is not { } pdCustom || pdCustom < 54 || pdCustom > 74)
                {
                    context.AddFailure(nameof(request.PupilDistanceMm), "PupilDistanceMm is required and must be within the standard 54-74mm range for a Custom LensRangeType (manual override outside this range is a Day 2 feature).");
                }

                break;
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

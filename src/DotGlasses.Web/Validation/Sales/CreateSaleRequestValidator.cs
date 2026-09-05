using DotGlasses.Application.Leads;
using DotGlasses.Application.ReferenceData;
using DotGlasses.Contracts.Sales;
using DotGlasses.Rules;
using FluentValidation;
using ContractLensRangeType = DotGlasses.Contracts.Common.LensRangeType;
using ReferenceDataCategory = DotGlasses.Domain.Enums.ReferenceDataCategory;

namespace DotGlasses.Web.Validation.Sales;

/// <summary>Lives in Web, not Contracts — see CreateTestRequestValidator's doc comment for why.</summary>
public class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator(IReferenceDataLookupService referenceData, IReferenceDataSnapshotProvider snapshots, ILeadRepository leadRepository)
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
            // Occupation, "referred or treated", frame colour and hard case (ticket 09) and now the
            // whole lens range (ticket 10) live in DotGlasses.Rules, which the Field App checks
            // against too; only the Coating set stays here, until ticket 11.
            //
            // Failure order is unchanged by this batch: the Coating set was already the last thing
            // each lens branch checked, so raising it after every lens-range failure is where it
            // landed before too.
            var snapshot = await snapshots.GetAsync(cancellationToken);
            foreach (var failure in ConsultationRules.Check(request, snapshot).Failures)
            {
                context.AddFailure(failure.Key, failure.Message);
            }

            await ValidateCoatingSetAsync(request, context, referenceData, cancellationToken);
            await ValidateSourceLeadAsync(request, context, leadRepository, cancellationToken);
        });
    }

    /// <summary>
    /// Ticket 11's remit, left in place deliberately, and the reason ticket 11 was blocked on this
    /// one: which Coatings are allowed depends on the lens branch. A preset range narrows them to
    /// those configured for the left lens option's strength; a Custom prescription accepts any
    /// active Coating. Re-deriving the branch here is the price of splitting the topic across two
    /// tickets — ticket 11 folds it into the shared module and the duplication goes away with it.
    ///
    /// The preset arm also re-tests all three preset ids, because ConsultationRules.PresetBranch
    /// short-circuits without them and this check has to stay silent in exactly the same cases:
    /// there is no left lens option to scope by, and reporting "choose at least one coating" at a
    /// technician who has not yet picked a lens would be noise on top of the real failure. A
    /// LensRangeType outside the enum reaches neither arm, exactly as before — RuleFor.IsInEnum
    /// above is what reports that.
    /// </summary>
    private static async Task ValidateCoatingSetAsync(
        CreateSaleRequest request, ValidationContext<CreateSaleRequest> context,
        IReferenceDataLookupService referenceData, CancellationToken cancellationToken)
    {
        if (request.LensRangeType is ContractLensRangeType.SixLensSet or ContractLensRangeType.NineLensSet)
        {
            if (request.PresetCatalogueId is not null && request.LensOptionRightId is not null && request.LensOptionLeftId is { } leftId)
            {
                await ValidateCoatingsAsync(request, context, referenceData, restrictToLensOptionId: leftId, cancellationToken);
            }
        }
        else if (request.LensRangeType == ContractLensRangeType.Custom)
        {
            await ValidateCoatingsAsync(request, context, referenceData, restrictToLensOptionId: null, cancellationToken);
        }
    }

    /// <summary>Shared by both the preset and Custom branches — coating pairing/exclusion rules
    /// apply universally, per ADR-0001. restrictToLensOptionId narrows to the coatings configured
    /// as available for that LensOption's strength (preset only); null means any active Coating
    /// (Custom).</summary>
    private static async Task ValidateCoatingsAsync(
        CreateSaleRequest request, ValidationContext<CreateSaleRequest> context,
        IReferenceDataLookupService referenceData, Guid? restrictToLensOptionId, CancellationToken cancellationToken)
    {
        if (request.CoatingRefIds.Count == 0)
        {
            context.AddFailure(nameof(request.CoatingRefIds), "Choose at least one coating.");
            return;
        }

        if (request.CoatingRefIds.Distinct().Count() != request.CoatingRefIds.Count)
        {
            context.AddFailure(nameof(request.CoatingRefIds), "CoatingRefIds must not contain duplicates.");
            return;
        }

        foreach (var coatingRefId in request.CoatingRefIds)
        {
            var lookup = await referenceData.LookupAsync(coatingRefId, ReferenceDataCategory.Coating, cancellationToken);
            if (lookup is not { IsActive: true })
            {
                context.AddFailure(nameof(request.CoatingRefIds), "CoatingRefIds must only reference existing, active Coating reference-data items.");
                return;
            }

            if (restrictToLensOptionId is { } lensOptionId && !await referenceData.IsCoatingAvailableForLensOptionAsync(lensOptionId, coatingRefId, cancellationToken))
            {
                context.AddFailure(nameof(request.CoatingRefIds), "Every coating must be configured as available for the chosen lens option (see Reference Data > Lens Strength).");
                return;
            }
        }

        for (var i = 0; i < request.CoatingRefIds.Count; i++)
        {
            for (var j = i + 1; j < request.CoatingRefIds.Count; j++)
            {
                if (await referenceData.AreCoatingsExcludedAsync(request.CoatingRefIds[i], request.CoatingRefIds[j], cancellationToken))
                {
                    context.AddFailure(nameof(request.CoatingRefIds), "This coating combination isn't allowed — two of the selected coatings exclude each other.");
                    return;
                }
            }
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

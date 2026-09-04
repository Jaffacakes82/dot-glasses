namespace DotGlasses.Application.Common;

/// <summary>Matches GenderMapping/LensRangeTypeMapping — Contracts keeps its own copy of the enum
/// so that DotGlasses.App never transitively pulls in Domain (see CLAUDE.md), and the Application
/// layer owns the translation.</summary>
public static class ReferenceDataCategoryMapping
{
    public static Contracts.Common.ReferenceDataCategory ToContract(this Domain.Enums.ReferenceDataCategory category) => category switch
    {
        Domain.Enums.ReferenceDataCategory.Occupation => Contracts.Common.ReferenceDataCategory.Occupation,
        Domain.Enums.ReferenceDataCategory.ReasonNotPurchased => Contracts.Common.ReferenceDataCategory.ReasonNotPurchased,
        Domain.Enums.ReferenceDataCategory.ReferralReason => Contracts.Common.ReferenceDataCategory.ReferralReason,
        Domain.Enums.ReferenceDataCategory.Coating => Contracts.Common.ReferenceDataCategory.Coating,
        Domain.Enums.ReferenceDataCategory.FrameColour => Contracts.Common.ReferenceDataCategory.FrameColour,
        Domain.Enums.ReferenceDataCategory.HardCaseColour => Contracts.Common.ReferenceDataCategory.HardCaseColour,
        Domain.Enums.ReferenceDataCategory.LensStrength => Contracts.Common.ReferenceDataCategory.LensStrength,
        Domain.Enums.ReferenceDataCategory.LensType => Contracts.Common.ReferenceDataCategory.LensType,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}

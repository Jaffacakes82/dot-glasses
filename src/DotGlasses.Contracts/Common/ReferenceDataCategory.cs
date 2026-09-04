namespace DotGlasses.Contracts.Common;

/// <summary>Mirrors DotGlasses.Domain.Enums.ReferenceDataCategory — see Contracts.Common.Gender
/// for why Contracts keeps its own copy rather than referencing Domain.</summary>
public enum ReferenceDataCategory
{
    Occupation = 0,
    ReasonNotPurchased = 1,
    ReferralReason = 2,
    Coating = 3,
    FrameColour = 4,
    HardCaseColour = 5,
    LensStrength = 6,
    LensType = 7,
}

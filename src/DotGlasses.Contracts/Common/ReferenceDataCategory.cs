namespace DotGlasses.Contracts.Common;

/// <summary>Mirrors DotGlasses.Domain.Enums.ReferenceDataCategory — see Contracts.Common.Gender
/// for why Contracts keeps its own copy rather than referencing Domain. Value 6 ("Lens strength")
/// is retired and reserved there and here alike (ADR-0007); never reuse it.</summary>
public enum ReferenceDataCategory
{
    Occupation = 0,
    ReasonNotPurchased = 1,
    ReferralReason = 2,
    Coating = 3,
    FrameColour = 4,
    HardCaseColour = 5,

    // 6 — retired ("Lens strength", ADR-0007). Reserved; do not reuse.

    LensType = 7,

    /// <summary>Children's frame colours; <see cref="FrameColour"/> is the adult list.</summary>
    FrameColourChild = 8,
}

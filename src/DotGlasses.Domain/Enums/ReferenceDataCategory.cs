namespace DotGlasses.Domain.Enums;

/// <summary>
/// One generic ReferenceDataItem table backs every admin-managed dropdown list rather than six
/// near-identical entities. Category correctness on FKs (e.g. LensOption.CoatingId must point at
/// a Coating row) is enforced in the Application layer, not the database — the standard trade-off
/// generic reference tables make.
/// </summary>
public enum ReferenceDataCategory
{
    Occupation = 0,
    ReasonNotPurchased = 1,
    ReferralReason = 2,
    Coating = 3,
    FrameColour = 4,
    HardCaseColour = 5,
}

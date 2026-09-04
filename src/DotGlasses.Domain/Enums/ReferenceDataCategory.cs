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

    /// <summary>Curated lens-power labels (e.g. "+2.50", "+0.00 / +2.50 Bifocal") an admin
    /// maintains directly — not yet consumed by PresetCatalogue/LensOption, which still define
    /// their own typed SphericalPower/IsBifocal/AddPower fields per row; see CLAUDE.md's [OPEN]
    /// items for rewiring that to build from this list instead.</summary>
    LensStrength = 6,

    /// <summary>Bifocal/Progressive/Other — asked on a custom lens when it carries two distinct
    /// powers (see the "ask lens type when two powers are present" agent brief).</summary>
    LensType = 7,
}

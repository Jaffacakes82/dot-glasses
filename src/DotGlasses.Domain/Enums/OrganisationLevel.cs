namespace DotGlasses.Domain.Enums;

/// <summary>
/// Only Dgi, Country and RetailPoint carry business rules (custom-order visibility, preset
/// catalogue ownership, etc.) — everything else in the tree (Distributor, Retailer, sub-reseller)
/// is Intermediate. The tree's actual depth beyond these anchors is arbitrary. A level is shown
/// to people through OrganisationLevelLabels, never as the enum member's name.
/// </summary>
public enum OrganisationLevel
{
    Dgi = 0,
    Country = 1,
    Intermediate = 2,
    RetailPoint = 3,
}

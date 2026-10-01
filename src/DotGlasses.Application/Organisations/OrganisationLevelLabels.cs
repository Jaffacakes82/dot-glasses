using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.Organisations;

/// <summary>The one place an <see cref="OrganisationLevel"/> becomes words a person reads — the
/// Organisations tree and panel, the Add dialog, the org picker and the CSV export all show a
/// level through this. Nothing may decide behaviour by comparing against the label: compare the
/// level itself.</summary>
public static class OrganisationLevelLabels
{
    public static string For(OrganisationLevel level) => level switch
    {
        OrganisationLevel.Dgi => "DGI",
        OrganisationLevel.Country => "Country",
        OrganisationLevel.Intermediate => "Retailer/distributor",
        OrganisationLevel.RetailPoint => "Retail Point",
        _ => level.ToString(),
    };
}

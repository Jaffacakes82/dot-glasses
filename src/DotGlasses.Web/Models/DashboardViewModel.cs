using DotGlasses.Application.Dashboard;

namespace DotGlasses.Web.Models;

/// <summary>
/// MI Reporting Dashboard, backed by IDashboardQueryService. The date range, Country and Retailer
/// narrow every tile and list; the "Top performing" lists rank by most sales or best conversion.
/// No "distribution by retail-point type" tile — no such concept exists in the domain.
/// </summary>
public class DashboardViewModel
{
    /// <summary>The value the Retailer dropdown posts for "the retail points with no Retailer".</summary>
    public const string NoRetailerValue = "none";

    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }

    public Guid? Country { get; init; }

    /// <summary>A Retailer's id, <see cref="NoRetailerValue"/>, or null for all.</summary>
    public string? Retailer { get; init; }

    public DashboardRanking Ranking { get; init; }

    public required IReadOnlyList<DashboardOrgOption> Countries { get; init; }
    public required IReadOnlyList<DashboardOrgOption> Retailers { get; init; }
    public bool HasNoRetailerOption { get; init; }

    public bool HasOrganisationFilter => Country is not null || !string.IsNullOrEmpty(Retailer);
    public bool HasAnyFilter => HasOrganisationFilter || FromDate.HasValue || ToDate.HasValue;

    public int PendingLeads { get; init; }
    public int TotalTests { get; init; }
    public int StandardSales { get; init; }
    public int CustomOrders { get; init; }
    public double TestToSaleConversion { get; init; }
    public double NeededToSaleConversion { get; init; }
    public int ReferralsLogged { get; init; }

    public required IReadOnlyList<int> ConversionTrend { get; init; }
    public int GenderMalePercent { get; init; }
    public int GenderFemalePercent { get; init; }

    /// <summary>The four "Top performing" lists, each with its heading.</summary>
    public required IReadOnlyList<(string Heading, IReadOnlyList<DashboardRankedEntry> Entries)> TopLists { get; init; }
}

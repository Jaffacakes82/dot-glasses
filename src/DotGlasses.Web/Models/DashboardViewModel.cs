namespace DotGlasses.Web.Models;

/// <summary>
/// Placeholder data for the MI Reporting Dashboard, matching the shape shown in the design
/// mockups (design/admin/data.js). Real domain entities (Test/Lead/Sale, org hierarchy) aren't
/// designed yet — see CLAUDE.md — so this is populated with static placeholder values in
/// HomeController rather than queried from a real data source. Replace with a real query once
/// those entities exist.
/// </summary>
public class DashboardViewModel
{
    public int PendingLeads { get; init; }
    public int TotalTests { get; init; }
    public int StandardSales { get; init; }
    public int CustomLenses { get; init; }
    public double TestToSaleConversion { get; init; }
    public double NeededToSaleConversion { get; init; }
    public int ReferralsLogged { get; init; }

    public required IReadOnlyList<int> ConversionTrend { get; init; }
    public int GenderMalePercent { get; init; }
    public int GenderFemalePercent { get; init; }
    public required IReadOnlyList<RetailPointShare> RetailPointDistribution { get; init; }

    public required IReadOnlyList<RankedEntry> TopOutlets { get; init; }
    public required IReadOnlyList<RankedEntry> TopRetailers { get; init; }
    public required IReadOnlyList<RankedEntry> TopCountries { get; init; }
    public required IReadOnlyList<RankedEntry> TopAgents { get; init; }
}

public record RetailPointShare(string Label, int Percent);

public record RankedEntry(string Name, int Sales, double ConversionPercent);

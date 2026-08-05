namespace DotGlasses.Web.Models;

/// <summary>
/// MI Reporting Dashboard, backed by IDashboardQueryService (2026-08-05 — see CLAUDE.md's Admin
/// Portal wiring section). No "distribution by retail-point type" tile — no such concept exists
/// in the domain, see IDashboardQueryService's own doc comment. No filters or a sales-vs-
/// conversion sort toggle either — top-N lists are a fixed sort by sales volume.
/// </summary>
public class DashboardViewModel
{
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

    public required IReadOnlyList<RankedEntry> TopOutlets { get; init; }
    public required IReadOnlyList<RankedEntry> TopRetailers { get; init; }
    public required IReadOnlyList<RankedEntry> TopCountries { get; init; }
    public required IReadOnlyList<RankedEntry> TopTechnicians { get; init; }
}

public record RankedEntry(string Name, int Sales, double ConversionPercent);

using System.Diagnostics;
using DotGlasses.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotGlasses.Web.Models;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class HomeController(IDashboardQueryService dashboardQueryService) : Controller
{
    /// <summary>
    /// The filters travel as query-string parameters. <paramref name="retailer"/> is a Retailer's
    /// id or "none" (the retail points that have no Retailer); anything else — a mistyped or
    /// stale value — matches nothing, so the page shows zeroes rather than quietly ignoring the
    /// filter. A Country or Retailer outside the caller's scope shows nothing for the same
    /// reason: none of the rows they can see sits under it.
    /// </summary>
    public async Task<IActionResult> Index(
        DateOnly? fromDate, DateOnly? toDate, string? country, string? retailer, DashboardRanking rank = DashboardRanking.MostSales,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtcExclusive) = DateRange.ToUtcRange(fromDate, toDate);

        // Bound as text, not as a Guid: an unreadable Guid would bind to null and quietly show
        // everything. Guid.Empty names no organisation, so it matches nothing.
        static Guid? IdOrNothing(string? value) =>
            string.IsNullOrEmpty(value) ? null : Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;

        var noRetailer = string.Equals(retailer, DashboardViewModel.NoRetailerValue, StringComparison.OrdinalIgnoreCase);
        var retailerId = noRetailer ? null : IdOrNothing(retailer);
        var countryId = IdOrNothing(country);

        var snapshot = await dashboardQueryService.GetAsync(
            new DashboardFilter(fromUtc, toUtcExclusive, countryId, retailerId, noRetailer, rank), cancellationToken);
        var figures = snapshot.Figures;

        var model = new DashboardViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            Country = countryId,
            Retailer = string.IsNullOrEmpty(retailer) ? null : retailer,
            Ranking = rank,
            Countries = snapshot.Countries,
            Retailers = snapshot.Retailers,
            HasNoRetailerOption = snapshot.HasNoRetailerOption,
            PendingLeads = figures.PendingLeads,
            TotalTests = figures.TotalTests,
            StandardSales = figures.StandardSales,
            CustomOrders = figures.CustomOrders,
            TestToSaleConversion = figures.TestToSaleConversionPercent,
            NeededToSaleConversion = figures.NeededToSaleConversionPercent,
            ReferralsLogged = figures.ReferralsLogged,
            ConversionTrend = figures.ConversionTrendPercent,
            GenderMalePercent = figures.GenderMalePercent,
            GenderFemalePercent = figures.GenderFemalePercent,
            TopLists =
            [
                ("Top outlets", figures.TopOutlets),
                ("Top retailers", figures.TopRetailers),
                ("Top countries", figures.TopCountries),
                ("Top technicians", figures.TopTechnicians),
            ],
        };

        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // The trace ID alone, not Activity.Id: Serilog's CompactJsonFormatter writes it as "@tr" on
        // every log line of the request, whereas Activity.Id is the full W3C traceparent
        // ("00-<trace>-<span>-<flags>"), which no log field contains verbatim. TraceIdentifier is
        // the fallback because it matches the "RequestId" property from ASP.NET Core's log scope.
        var traceId = Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity
            ? activity.TraceId.ToHexString()
            : HttpContext.TraceIdentifier;

        return View(new ErrorViewModel { TraceId = traceId });
    }
}

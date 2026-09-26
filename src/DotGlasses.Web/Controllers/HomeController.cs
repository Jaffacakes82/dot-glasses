using System.Diagnostics;
using DotGlasses.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotGlasses.Web.Models;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class HomeController(IDashboardQueryService dashboardQueryService) : Controller
{
    public async Task<IActionResult> Index(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtcExclusive) = DateRange.ToUtcRange(fromDate, toDate);
        var snapshot = await dashboardQueryService.GetAsync(fromUtc, toUtcExclusive, cancellationToken);

        var model = new DashboardViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            PendingLeads = snapshot.PendingLeads,
            TotalTests = snapshot.TotalTests,
            StandardSales = snapshot.StandardSales,
            CustomOrders = snapshot.CustomOrders,
            TestToSaleConversion = snapshot.TestToSaleConversionPercent,
            NeededToSaleConversion = snapshot.NeededToSaleConversionPercent,
            ReferralsLogged = snapshot.ReferralsLogged,
            ConversionTrend = snapshot.ConversionTrendPercent,
            GenderMalePercent = snapshot.GenderMalePercent,
            GenderFemalePercent = snapshot.GenderFemalePercent,
            TopOutlets = snapshot.TopOutlets.Select(e => new RankedEntry(e.Name, e.Sales, e.ConversionPercent)).ToList(),
            TopRetailers = snapshot.TopRetailers.Select(e => new RankedEntry(e.Name, e.Sales, e.ConversionPercent)).ToList(),
            TopCountries = snapshot.TopCountries.Select(e => new RankedEntry(e.Name, e.Sales, e.ConversionPercent)).ToList(),
            TopTechnicians = snapshot.TopTechnicians.Select(e => new RankedEntry(e.Name, e.Sales, e.ConversionPercent)).ToList(),
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

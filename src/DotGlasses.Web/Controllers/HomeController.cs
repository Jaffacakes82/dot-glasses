using System.Diagnostics;
using DotGlasses.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotGlasses.Web.Models;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class HomeController(IDashboardQueryService dashboardQueryService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var snapshot = await dashboardQueryService.GetAsync(cancellationToken);

        var model = new DashboardViewModel
        {
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
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

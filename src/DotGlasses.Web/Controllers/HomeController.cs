using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DotGlasses.Web.Models;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        var model = new DashboardViewModel
        {
            PendingLeads = 42,
            TotalTests = 318,
            StandardSales = 176,
            CustomLenses = 23,
            TestToSaleConversion = 55.3,
            NeededToSaleConversion = 71.8,
            ReferralsLogged = 19,
            ConversionTrend = [38, 44, 41, 52, 49, 55],
            GenderMalePercent = 46,
            GenderFemalePercent = 54,
            RetailPointDistribution =
            [
                new RetailPointShare("Independent retailers", 62),
                new RetailPointShare("Faith-affiliated networks", 24),
                new RetailPointShare("Community outreach", 14),
            ],
            TopOutlets =
            [
                new RankedEntry("Kangemi Vision Centre", 41, 68.2),
                new RankedEntry("St. Angela Marillac / Kangemi", 33, 61.0),
                new RankedEntry("Nakuru Central", 29, 58.4),
            ],
            TopRetailers =
            [
                new RankedEntry("Classical Optician", 112, 63.1),
                new RankedEntry("Diocese of Nakuru Network", 74, 55.9),
            ],
            TopCountries =
            [
                new RankedEntry("Kenya", 176, 59.2),
                new RankedEntry("Uganda", 98, 51.7),
                new RankedEntry("Tanzania", 44, 47.3),
            ],
            TopAgents =
            [
                new RankedEntry("A. Wanjiru", 27, 72.4),
                new RankedEntry("J. Otieno", 24, 65.0),
            ],
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

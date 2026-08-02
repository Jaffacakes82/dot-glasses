using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

public class EventHistoryController : Controller
{
    public IActionResult Index(string tab = "sales")
    {
        var model = new EventHistoryViewModel
        {
            ActiveTab = tab,
            Events =
            [
                new SaleOrTestEvent("Sale", false, "Wanjiru M.", "Kangemi Vision Centre", "Kenya", "2026-08-01 14:20"),
                new SaleOrTestEvent("Sale", true, "Otieno K.", "Nakuru Central", "Kenya", "2026-08-01 11:05"),
                new SaleOrTestEvent("Test", false, "Achieng P.", "Kampala Optics Group", "Uganda", "2026-07-31 16:40"),
            ],
            Leads =
            [
                new LeadEvent("Wanjiru M.", "+254 7•• •••012", "Kangemi Vision Centre", "Price", "2026-08-01"),
                new LeadEvent("Kamau S.", "+254 7•• •••588", "Nakuru Central", "Wanted to think about it", "2026-07-30"),
            ],
            Referrals =
            [
                new ReferralEvent("Kangemi Vision Centre", "Kenya", "Suspected cataract", "2026-07-29 09:15"),
            ],
        };

        return View(model);
    }
}

using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

public class CustomOrdersController : Controller
{
    public IActionResult Index()
    {
        var orders = new List<CustomOrder>
        {
            new("Otieno K.", "Nakuru Central", "OD -4.50 / OS -4.25, cyl -0.75", "In Lab"),
            new("Mwangi T.", "Kangemi Vision Centre", "OD -2.00 / OS -2.25", "Submitted"),
            new("Achieng P.", "Kampala Optics Group", "OD +1.50 / OS +1.75", "Ready for Pickup"),
        };

        return View(orders);
    }
}

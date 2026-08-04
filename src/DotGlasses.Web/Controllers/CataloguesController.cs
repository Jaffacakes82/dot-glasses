using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.PresetCatalogueManage)]
public class CataloguesController : Controller
{
    public IActionResult Index()
    {
        var catalogues = new List<Catalogue>
        {
            new("6-Lens Set", "Standard six-option lens range for outlets with local stock.", "+2.50 to -4.50", 8),
            new("Community Essentials", "A short, stock frame + reading-strength range for outreach settings.", "+1.00 to +3.00", 8),
        };

        return View(catalogues);
    }
}

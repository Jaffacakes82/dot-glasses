using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class OrganisationsController : Controller
{
    public IActionResult Index()
    {
        var tree = new OrgNode("dgi", "DOT Glasses International", "DGI", Catalogue: null, Kind: null, Children:
        [
            new OrgNode("kenya", "Kenya", "Country", Catalogue: null, Kind: null, Children:
            [
                new OrgNode("nairobi-retail-group", "Nairobi Retail Group", "Retailer", "6-Lens Set", Kind: null, Children:
                [
                    new OrgNode("kangemi", "Kangemi Vision Centre", "RetailPoint", "6-Lens Set", "Standalone", []),
                    new OrgNode("nakuru-central", "Nakuru Central", "RetailPoint", "6-Lens Set", "Standalone", []),
                ]),
                new OrgNode("diocese-nakuru", "Diocese of Nakuru Network", "Retailer", "Community Essentials", "Affiliated network", Children:
                [
                    new OrgNode("st-angela", "St. Angela Marillac Hospital / Kangemi", "RetailPoint", "Community Essentials", "Affiliated", []),
                ]),
            ]),
            new OrgNode("uganda", "Uganda", "Country", Catalogue: null, Kind: null, Children:
            [
                new OrgNode("kampala-optics", "Kampala Optics Group", "Retailer", "9-Lens Set", Kind: null, Children: []),
            ]),
        ]);

        return View(tree);
    }
}

using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

public class OrganisationsController : Controller
{
    public IActionResult Index()
    {
        var tree = new OrgNode("dgi", "DOT Glasses International", "DGI", Catalogue: null, Kind: null, Children:
        [
            new OrgNode("kenya", "Kenya", "Country", Catalogue: null, Kind: null, Children:
            [
                new OrgNode("classical-optician", "Classical Optician", "Retailer", "Classical Optician", Kind: null, Children:
                [
                    new OrgNode("kangemi", "Kangemi Vision Centre", "RetailPoint", "Classical Optician", "Standalone", []),
                    new OrgNode("nakuru-central", "Nakuru Central", "RetailPoint", "Classical Optician", "Standalone", []),
                ]),
                new OrgNode("diocese-nakuru", "Diocese of Nakuru Network", "Retailer", "Community Essentials", "Affiliated network", Children:
                [
                    new OrgNode("st-angela", "St. Angela Marillac Hospital / Kangemi", "RetailPoint", "Community Essentials", "Affiliated", []),
                ]),
            ]),
            new OrgNode("uganda", "Uganda", "Country", Catalogue: null, Kind: null, Children:
            [
                new OrgNode("kampala-optics", "Kampala Optics Group", "Retailer", "Classical Optician", Kind: null, Children: []),
            ]),
        ]);

        return View(tree);
    }
}

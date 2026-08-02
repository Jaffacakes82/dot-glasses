using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

public class ReferenceDataController : Controller
{
    public IActionResult Index()
    {
        var lists = new List<ReferenceDataList>
        {
            new("Reasons not purchased", "DGI-editable · shown in the field app Lead form", ["Price", "Wanted to think about it", "Didn't like frame options", "Out of stock"]),
            new("Referral reasons", "DGI-editable · shown in the field app Test form", ["Suspected cataract", "Suspected glaucoma", "Other eye disease"]),
            new("Coating preferences", "DGI-editable · shown in the consultation form", ["Anti-glare", "Scratch-resistant", "UV protection"]),
            new("Tint options", "DGI-editable · shown in the consultation form", ["None", "Light", "Photochromic"]),
            new("Frame colors", "DGI-editable · shown in the consultation form", ["Black", "Tortoise", "Navy", "Clear", "Rose Gold"]),
            new("Occupations", "DGI-editable · shown in the Sale form", ["Farmer", "Teacher", "Driver", "Trader", "Student"]),
        };

        return View(lists);
    }
}

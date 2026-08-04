using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize]
public class UserDirectoryController : Controller
{
    private static readonly Dictionary<string, string> StatusColor = new()
    {
        ["Active"] = "var(--dot-green)",
        ["Invited"] = "var(--dot-yellow)",
        ["Inactive"] = "#cccccc",
    };

    public IActionResult Index()
    {
        ViewData["StatusColor"] = StatusColor;

        var users = new List<DirectoryUser>
        {
            new("A. Wanjiru", "Manager", ["Kenya"], "Active", "2026-08-01", "27"),
            new("J. Otieno", "User", ["Kangemi Vision Centre"], "Active", "2026-07-31", "24"),
            new("S. Kamau", "User", ["Nakuru Central", "Kangemi Vision Centre"], "Invited", "—", "—"),
            new("Admin Office", "Admin", ["DOT Glasses International"], "Active", "2026-08-02", "—"),
        };

        return View(users);
    }
}

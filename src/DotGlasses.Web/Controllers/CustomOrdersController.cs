using DotGlasses.Application.CustomOrders;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.CustomOrdersView)]
public class CustomOrdersController(ICustomOrderService customOrderService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var orders = await customOrderService.ListAsync(cancellationToken);
        return View(orders.Select(o => new CustomOrder(o.SaleId, o.CustomerName, o.Outlet, o.Prescription, o.Status)).ToList());
    }

    /// <summary>Reuses the page-level CustomOrdersView policy for the write action too (2026-08-05
    /// decision) — any role, Country level and above, same gate that already controls seeing the
    /// queue at all.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceStatus(Guid saleId, CancellationToken cancellationToken)
    {
        await customOrderService.AdvanceStatusAsync(saleId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}

using DotGlasses.Application.CustomOrders;
using DotGlasses.Domain.Enums;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.CustomOrdersView)]
public class CustomOrdersController(ICustomOrderService customOrderService) : Controller
{
    public async Task<IActionResult> Index(FulfilmentStatus? status, CancellationToken cancellationToken = default)
    {
        var grouped = await customOrderService.ListGroupedAsync(status, cancellationToken);

        return View(new CustomOrdersViewModel
        {
            Retailers = grouped.Retailers.Select(ToWebModel).ToList(),
            Status = status,
            TotalCount = grouped.TotalCount,
        });
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

    private static RetailerGroup ToWebModel(RetailerOrderGroup g) =>
        new(g.RetailerName, g.ActiveCount, g.RetailPoints.Select(ToWebModel).ToList());

    private static RetailPointGroup ToWebModel(RetailPointOrderGroup g) =>
        new(g.RetailPointName, g.ActiveCount, g.Customers.Select(ToWebModel).ToList());

    private static CustomerGroup ToWebModel(CustomerOrderGroup g) =>
        new(g.CustomerName, g.Orders.Select(o => new CustomOrder(o.SaleId, o.CustomerName, o.Outlet, o.Prescription, o.Status)).ToList());
}

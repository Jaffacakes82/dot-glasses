using System.Globalization;
using DotGlasses.Application.CustomOrders;
using DotGlasses.Domain.Enums;
using DotGlasses.Web.Authorization;
using DotGlasses.Web.Export;
using DotGlasses.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotGlasses.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.CustomOrdersView)]
public class CustomOrdersController(ICustomOrderService customOrderService) : Controller
{
    private const int PageSize = 25;

    public async Task<IActionResult> Index(FulfilmentStatus? status, int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        var ordersPage = await customOrderService.ListAsync(status, page, PageSize, cancellationToken);

        return View(new CustomOrdersViewModel
        {
            Orders = ordersPage.Items.Select(o => new CustomOrder(o.SaleId, o.CustomerName, o.Outlet, o.Prescription, o.Status)).ToList(),
            Status = status,
            Page = page,
            PageSize = PageSize,
            TotalCount = ordersPage.TotalCount,
            TotalPages = ordersPage.TotalPages,
        });
    }

    /// <summary>Drives off the same ExportAsync method as Index's ListAsync — same status filter
    /// and scoping, just unpaged — reuses the class-level CustomOrdersView policy, same as
    /// Index.</summary>
    public async Task<IActionResult> Export(FulfilmentStatus? status, CancellationToken cancellationToken)
    {
        var orders = await customOrderService.ExportAsync(status, cancellationToken);
        var csv = CsvExport.Build(
            ["Customer", "Outlet", "Prescription", "Status", "ConsentGiven", "Created"],
            orders.Select(o => (IReadOnlyList<string?>)[o.CustomerName, o.Outlet, o.Prescription, o.Status.ToString(), o.ConsentGiven.ToString(), o.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)]));

        return File(csv, "text/csv", $"custom-orders-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
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

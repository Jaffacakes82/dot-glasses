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
    public async Task<IActionResult> Index(FulfilmentStatus? status, CancellationToken cancellationToken = default) =>
        View(await BuildViewModelAsync(status, cancellationToken));

    /// <summary>Drives off ExportAsync — same status filter and scoping as Index's
    /// ListGroupedAsync, just unpaged and flat rather than grouped — reuses the class-level
    /// CustomOrdersView policy, same as Index.</summary>
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
        // Already-Fulfilled (a colleague got there first, a double click, a browser resubmit),
        // not-a-custom-order and out-of-scope all leave the service as DomainRuleViolationException
        // and are rendered inline by DomainRuleViolationFilter — no catch here, deliberately
        // (ADR-0003: a controller that catches one is the pattern the filter exists to remove).
        await customOrderService.AdvanceStatusAsync(saleId, cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    private async Task<CustomOrdersViewModel> BuildViewModelAsync(FulfilmentStatus? status, CancellationToken cancellationToken)
    {
        var grouped = await customOrderService.ListGroupedAsync(status, cancellationToken);

        return new CustomOrdersViewModel
        {
            Retailers = grouped.Retailers.Select(ToWebModel).ToList(),
            Status = status,
            TotalCount = grouped.TotalCount,
        };
    }

    private static RetailerGroup ToWebModel(RetailerOrderGroup g) =>
        new(g.RetailerName, g.ActiveCount, g.RetailPoints.Select(ToWebModel).ToList());

    private static RetailPointGroup ToWebModel(RetailPointOrderGroup g) =>
        new(g.RetailPointName, g.ActiveCount, g.Customers.Select(ToWebModel).ToList());

    private static CustomerGroup ToWebModel(CustomerOrderGroup g) =>
        new(g.CustomerName, g.Orders.Select(o => new CustomOrder(o.SaleId, o.CustomerName, o.Outlet, o.Prescription, o.Status)).ToList());
}

using DotGlasses.Application.CustomOrders;
using DotGlasses.Domain.Entities;
using DomainFulfilmentStatus = DotGlasses.Domain.Enums.FulfilmentStatus;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — matches
/// EventHistoryQueryService/PresetCatalogueAdminService (a bespoke read + one write action, no
/// repository interface needed for this shape).</summary>
public class CustomOrderService(DotGlassesDbContext dbContext) : ICustomOrderService
{
    public async Task<CustomOrderGroupedResult> ListGroupedAsync(DomainFulfilmentStatus? status, CancellationToken cancellationToken = default)
    {
        var allOrders = await dbContext.Sales.Where(x => x.FulfilmentStatus != null).ToListAsync(cancellationToken);
        var enriched = await EnrichAsync(allOrders, cancellationToken);

        // Computed from the caller's entire scoped order set, not `visible` below — see
        // ICustomOrderService's doc comment for why this deliberately ignores the status filter.
        var activeCountsByRetailer = enriched
            .Where(e => IsActive(e.Sale.FulfilmentStatus!.Value))
            .GroupBy(e => e.RetailerId)
            .ToDictionary(g => g.Key, g => g.Count());
        var activeCountsByRetailPoint = enriched
            .Where(e => IsActive(e.Sale.FulfilmentStatus!.Value))
            .GroupBy(e => (e.RetailerId, e.RetailPointId))
            .ToDictionary(g => g.Key, g => g.Count());

        var visible = (status is { } value ? enriched.Where(e => e.Sale.FulfilmentStatus == value) : enriched).ToList();

        var retailers = visible
            .GroupBy(e => e.RetailerId)
            .OrderBy(g => g.First().RetailerName, StringComparer.OrdinalIgnoreCase)
            .Select(retailerGroup => new RetailerOrderGroup(
                retailerGroup.Key,
                retailerGroup.First().RetailerName,
                activeCountsByRetailer.GetValueOrDefault(retailerGroup.Key),
                retailerGroup
                    .GroupBy(e => e.RetailPointId)
                    .OrderBy(g => g.First().RetailPointName, StringComparer.OrdinalIgnoreCase)
                    .Select(retailPointGroup => new RetailPointOrderGroup(
                        retailPointGroup.Key,
                        retailPointGroup.First().RetailPointName,
                        activeCountsByRetailPoint.GetValueOrDefault((retailerGroup.Key, retailPointGroup.Key)),
                        retailPointGroup
                            .GroupBy(e => e.CustomerId)
                            .OrderBy(g => g.First().CustomerName, StringComparer.OrdinalIgnoreCase)
                            .Select(customerGroup => new CustomerOrderGroup(
                                customerGroup.Key,
                                customerGroup.First().CustomerName,
                                customerGroup.Select(ToRow).OrderByDescending(r => r.CreatedAtUtc).ToList()))
                            .ToList()))
                    .ToList()))
            .ToList();

        return new CustomOrderGroupedResult(retailers, visible.Count);
    }

    /// <summary>Export variant of ListGroupedAsync — same status filter and scoping (shares
    /// EnrichAsync with the grouped list), unpaged and flat rather than grouped, so the CSV
    /// export drives off the same underlying filtered data the on-screen list uses.</summary>
    public async Task<IReadOnlyList<CustomOrderRow>> ExportAsync(DomainFulfilmentStatus? status, CancellationToken cancellationToken = default)
    {
        var allOrders = await dbContext.Sales.Where(x => x.FulfilmentStatus != null).ToListAsync(cancellationToken);
        var enriched = await EnrichAsync(allOrders, cancellationToken);
        var visible = status is { } value ? enriched.Where(e => e.Sale.FulfilmentStatus == value) : enriched;
        return visible.Select(ToRow).OrderByDescending(r => r.CreatedAtUtc).ToList();
    }

    public async Task AdvanceStatusAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await dbContext.Sales.FirstAsync(x => x.Id == saleId, cancellationToken);
        if (sale.FulfilmentStatus is not { } current)
        {
            throw new InvalidOperationException("This Sale is not a custom order routed to fulfilment.");
        }

        sale.FulfilmentStatus = current switch
        {
            DomainFulfilmentStatus.Submitted => DomainFulfilmentStatus.InLab,
            DomainFulfilmentStatus.InLab => DomainFulfilmentStatus.ReadyForPickup,
            DomainFulfilmentStatus.ReadyForPickup => DomainFulfilmentStatus.Fulfilled,
            DomainFulfilmentStatus.Fulfilled => throw new InvalidOperationException("This custom order is already Fulfilled."),
            _ => throw new ArgumentOutOfRangeException(nameof(current), current, null),
        };

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Resolves each order's retail point and "retailer" (the retail point's immediate
    /// parent node — see RetailPointOrderGroup's doc comment) via a plain scoped
    /// OrganisationNodes query, not IUnscopedReportQueryService: CustomOrdersView requires Country
    /// level+, so both nodes are always within the caller's own subtree, never above it — the
    /// ancestor-resolution pitfall (CLAUDE.md) doesn't apply here.</summary>
    private async Task<List<EnrichedOrder>> EnrichAsync(List<Sale> orders, CancellationToken cancellationToken)
    {
        var customerIds = orders.Select(s => s.CustomerId).Distinct().ToList();
        var customers = await dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var orgNodes = await dbContext.OrganisationNodes.ToListAsync(cancellationToken);
        var orgByPath = orgNodes.ToDictionary(n => n.HierarchyPath);
        var orgById = orgNodes.ToDictionary(n => n.Id);

        return orders.Select(s =>
        {
            orgByPath.TryGetValue(s.HierarchyPath, out var retailPoint);
            var retailer = retailPoint?.ParentId is { } parentId && orgById.TryGetValue(parentId, out var parent) ? parent : null;
            var customer = customers.GetValueOrDefault(s.CustomerId);
            return new EnrichedOrder(
                s,
                retailer?.Id ?? Guid.Empty, retailer?.Name ?? "Unknown retailer",
                retailPoint?.Id ?? Guid.Empty, retailPoint?.Name ?? "Unknown outlet",
                s.CustomerId, customer?.FullName ?? "—");
        }).ToList();
    }

    private static CustomOrderRow ToRow(EnrichedOrder e) => new(
        e.Sale.Id,
        e.CustomerName,
        e.RetailPointName,
        FormatPrescription(e.Sale),
        e.Sale.FulfilmentStatus!.Value,
        e.Sale.CreatedAtUtc,
        e.Sale.ConsentGiven);

    private static bool IsActive(DomainFulfilmentStatus status) => status != DomainFulfilmentStatus.Fulfilled;

    private static string FormatPrescription(Sale s) =>
        $"OD {FormatEye(s.CustomSphereRight, s.CustomCylinderRight, s.CustomAddPowerRight)} / OS {FormatEye(s.CustomSphereLeft, s.CustomCylinderLeft, s.CustomAddPowerLeft)}";

    private static string FormatEye(decimal? sphere, decimal? cylinder, decimal? addPower)
    {
        var parts = new List<string> { FormatPower(sphere ?? 0m) };
        if (cylinder is { } cyl && cyl != 0m)
        {
            parts.Add($"cyl {FormatPower(cyl)}");
        }

        if (addPower is { } add && add != 0m)
        {
            parts.Add($"add {FormatPower(add)}");
        }

        return string.Join(" ", parts);
    }

    private static string FormatPower(decimal v) => v >= 0 ? $"+{v:0.00}" : v.ToString("0.00");

    private sealed record EnrichedOrder(Sale Sale, Guid RetailerId, string RetailerName, Guid RetailPointId, string RetailPointName, Guid CustomerId, string CustomerName);
}

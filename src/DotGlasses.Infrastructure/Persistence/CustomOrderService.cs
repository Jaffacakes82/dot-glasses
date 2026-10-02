using DotGlasses.Application.CustomOrders;
using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Common;
using DotGlasses.Domain.Entities;
using DomainFulfilmentStatus = DotGlasses.Domain.Enums.FulfilmentStatus;
using Microsoft.EntityFrameworkCore;

namespace DotGlasses.Infrastructure.Persistence;

/// <summary>Queries DotGlassesDbContext directly rather than through a repository — matches
/// EventHistoryQueryService/PresetCatalogueAdminService (a bespoke read + one write action, no
/// repository interface needed for this shape). Retailer/outlet resolution is OrgTreeLookup's,
/// shared with the Dashboard and Event History rather than answered a second way here
/// (docs/adr/0004).
///
/// The queue is the CustomOrder records the caller can see (ADR-0008). An order holds no copy of
/// what was ordered: the lens and the customer are read through the record that placed it — the
/// Lead for an order a Lead placed, the Sale otherwise.</summary>
public class CustomOrderService(DotGlassesDbContext dbContext, IUnscopedReportQueryService unscopedReportQueryService) : ICustomOrderService
{
    public async Task<CustomOrderGroupedResult> ListGroupedAsync(DomainFulfilmentStatus? status, CancellationToken cancellationToken = default)
    {
        var enriched = await EnrichAsync(await dbContext.CustomOrders.ToListAsync(cancellationToken), cancellationToken);

        // Computed from the caller's entire scoped order set, not `visible` below — see
        // ICustomOrderService's doc comment for why this deliberately ignores the status filter.
        var activeCountsByRetailer = enriched
            .Where(e => IsActive(e.Order.Status))
            .GroupBy(e => e.Retailer)
            .ToDictionary(g => g.Key, g => g.Count());
        var activeCountsByRetailPoint = enriched
            .Where(e => IsActive(e.Order.Status))
            .GroupBy(e => (e.Retailer, e.RetailPointId))
            .ToDictionary(g => g.Key, g => g.Count());

        var visible = (status is { } value ? enriched.Where(e => e.Order.Status == value) : enriched).ToList();

        var retailers = visible
            .GroupBy(e => e.Retailer)
            .OrderBy(g => g.First().RetailerName, StringComparer.OrdinalIgnoreCase)
            .Select(retailerGroup => new RetailerOrderGroup(
                retailerGroup.Key.Id,
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
                                customerGroup.Select(ToRow).OrderByDescending(r => r.PlacedAtUtc).ToList()))
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
        var enriched = await EnrichAsync(await dbContext.CustomOrders.ToListAsync(cancellationToken), cancellationToken);
        var visible = status is { } value ? enriched.Where(e => e.Order.Status == value) : enriched;
        return visible.Select(ToRow).OrderByDescending(r => r.PlacedAtUtc).ToList();
    }

    /// <summary>Every rejection here is a DomainRuleViolationException carrying user-facing copy,
    /// surfaced inline by DomainRuleViolationFilter (ADR-0003) — including the "no such order"
    /// case, which is a deliberate conversion rather than a leaked missing row: the scoped query
    /// silently returns nothing for an order outside the caller's subtree, so an out-of-scope
    /// order and a nonexistent one are indistinguishable here by design, and must stay that way
    /// or the screen leaks which orders exist elsewhere in the tree. Both get the same sentence.
    /// The general-purpose InvalidOperationException still means "missing row or bug" everywhere
    /// it is left in place — see UserAdminService's "User not found." for that case.</summary>
    public async Task AdvanceStatusAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.CustomOrders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new DomainRuleViolationException("This custom order is no longer available.");
        }

        order.Status = order.Status switch
        {
            DomainFulfilmentStatus.Submitted => DomainFulfilmentStatus.InLab,
            DomainFulfilmentStatus.InLab => DomainFulfilmentStatus.ReadyForPickup,
            DomainFulfilmentStatus.ReadyForPickup => DomainFulfilmentStatus.Fulfilled,
            DomainFulfilmentStatus.Fulfilled => throw new DomainRuleViolationException("This custom order is already Fulfilled."),
            _ => throw new ArgumentOutOfRangeException(nameof(orderId), order.Status, null),
        };

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Resolves each order's retail point and Retailer through OrgTreeLookup, so this
    /// screen and the Dashboard name the same Retailer for the same retail point: the nearest
    /// Intermediate-level ancestor (CONTEXT.md), not the retail point's immediate parent node,
    /// which the two definitions disagree about whenever a retail point hangs directly off a
    /// Country.
    ///
    /// Fed from IUnscopedReportQueryService, not a plain scoped OrganisationNodes query: naming an
    /// ancestor is an ancestor lookup whatever level CustomOrdersView admits today (CLAUDE.md's
    /// "Data scoping vs RBAC" rule), and the policy is free to change without anyone noticing this
    /// depended on it.</summary>
    private async Task<List<EnrichedOrder>> EnrichAsync(List<CustomOrder> orders, CancellationToken cancellationToken)
    {
        // The placing records sit at the order's own retail point, so the scoped queries that
        // found the orders find them too.
        var leadIds = orders.Where(o => o.LeadId is not null).Select(o => o.LeadId!.Value).ToList();
        var saleIds = orders.Where(o => o.SaleId is not null).Select(o => o.SaleId!.Value).ToList();
        var leads = await dbContext.Leads.Where(l => leadIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);
        var sales = await dbContext.Sales.Where(s => saleIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);

        var placed = orders.ToDictionary(o => o.Id, o => PlacedBy(o, leads, sales));

        var customerIds = placed.Values.Where(p => p is not null).Select(p => p!.CustomerId).Distinct().ToList();
        var customers = await dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var orgLookup = new OrgTreeLookup(await unscopedReportQueryService.GetOrganisationNodesForReportsAsync(cancellationToken));

        return orders.Select(o =>
        {
            var retailer = orgLookup.RowRetailer(o.HierarchyPath);
            var retailPoint = orgLookup.RowOutlet(o.HierarchyPath);
            var by = placed[o.Id];
            var customer = by is null ? null : customers.GetValueOrDefault(by.CustomerId);
            return new EnrichedOrder(
                o,
                new RetailerKey(retailer.Kind, retailer.Node?.Id ?? Guid.Empty), retailer.Name,
                retailPoint?.Id ?? Guid.Empty, retailPoint?.ReportName ?? OrgTreeLookup.UnknownOutlet,
                by?.CustomerId ?? Guid.Empty, customer?.FullName ?? "—",
                by?.Prescription ?? "—", by?.ConsentGiven ?? false);
        }).ToList();
    }

    /// <summary>What an order reads from the record that placed it: the Lead when a Lead placed
    /// it, otherwise the Sale. Null only if that record is gone, which nothing does.</summary>
    private static PlacingRecord? PlacedBy(CustomOrder order, Dictionary<Guid, Lead> leads, Dictionary<Guid, Sale> sales)
    {
        if (order.LeadId is { } leadId && leads.TryGetValue(leadId, out var lead))
        {
            return new PlacingRecord(
                lead.CustomerId,
                FormatPrescription(lead.SphereLeft, lead.CylinderLeft, lead.AddLeft, lead.SphereRight, lead.CylinderRight, lead.AddRight),
                lead.ConsentGiven);
        }

        if (order.SaleId is { } saleId && sales.TryGetValue(saleId, out var sale))
        {
            return new PlacingRecord(
                sale.CustomerId,
                FormatPrescription(sale.SphereLeft, sale.CylinderLeft, sale.AddLeft, sale.SphereRight, sale.CylinderRight, sale.AddRight),
                sale.ConsentGiven);
        }

        return null;
    }

    private static CustomOrderRow ToRow(EnrichedOrder e) => new(
        e.Order.Id,
        e.CustomerName,
        e.RetailPointName,
        e.Prescription,
        e.Order.Status,
        e.Order.PlacedAtUtc,
        e.ConsentGiven,
        IsPaid: e.Order.SaleId is not null);

    private static bool IsActive(DomainFulfilmentStatus status) => status != DomainFulfilmentStatus.Fulfilled;

    /// <summary>Right eye (OD) then left (OS), as this screen has always written an order.</summary>
    private static string FormatPrescription(
        decimal? sphereLeft, decimal? cylinderLeft, decimal? addLeft,
        decimal? sphereRight, decimal? cylinderRight, decimal? addRight) =>
        $"OD {FormatEye(sphereRight, cylinderRight, addRight)} / OS {FormatEye(sphereLeft, cylinderLeft, addLeft)}";

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

    /// <summary>What the retailer tier groups on. The Retailer node's Id when there is one; when
    /// there isn't, the resolution kind carries the group instead, because "this retail point sits
    /// directly under a Country and so has no Retailer" and "this path is not a node in the tree
    /// at all" are different facts (CONTEXT.md) and must not collapse into the one Guid.Empty
    /// bucket the old immediate-parent fallback gave them both.</summary>
    private sealed record RetailerKey(RetailerResolutionKind Kind, Guid Id);

    private sealed record PlacingRecord(Guid CustomerId, string Prescription, bool ConsentGiven);

    private sealed record EnrichedOrder(
        CustomOrder Order, RetailerKey Retailer, string RetailerName, Guid RetailPointId, string RetailPointName,
        Guid CustomerId, string CustomerName, string Prescription, bool ConsentGiven);
}

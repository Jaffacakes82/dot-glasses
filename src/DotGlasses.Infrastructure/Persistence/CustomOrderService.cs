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
/// (docs/adr/0004).</summary>
public class CustomOrderService(DotGlassesDbContext dbContext, IUnscopedReportQueryService unscopedReportQueryService) : ICustomOrderService
{
    public async Task<CustomOrderGroupedResult> ListGroupedAsync(DomainFulfilmentStatus? status, CancellationToken cancellationToken = default)
    {
        var allOrders = await dbContext.Sales.Where(x => x.FulfilmentStatus != null).ToListAsync(cancellationToken);
        var enriched = await EnrichAsync(allOrders, cancellationToken);

        // Computed from the caller's entire scoped order set, not `visible` below — see
        // ICustomOrderService's doc comment for why this deliberately ignores the status filter.
        var activeCountsByRetailer = enriched
            .Where(e => IsActive(e.Sale.FulfilmentStatus!.Value))
            .GroupBy(e => e.Retailer)
            .ToDictionary(g => g.Key, g => g.Count());
        var activeCountsByRetailPoint = enriched
            .Where(e => IsActive(e.Sale.FulfilmentStatus!.Value))
            .GroupBy(e => (e.Retailer, e.RetailPointId))
            .ToDictionary(g => g.Key, g => g.Count());

        var visible = (status is { } value ? enriched.Where(e => e.Sale.FulfilmentStatus == value) : enriched).ToList();

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

    /// <summary>Every rejection here is a DomainRuleViolationException carrying user-facing copy,
    /// surfaced inline by DomainRuleViolationFilter (ADR-0003) — including the "no such order"
    /// case, which is a deliberate conversion rather than a leaked missing row: the scoped Sales
    /// query silently returns nothing for a sale outside the caller's subtree, so an out-of-scope
    /// order and a nonexistent one are indistinguishable here by design, and must stay that way
    /// or the screen leaks which sales exist elsewhere in the tree. Both get the same sentence.
    /// The general-purpose InvalidOperationException still means "missing row or bug" everywhere
    /// it is left in place — see UserOrgAssignmentService for that case.</summary>
    public async Task AdvanceStatusAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await dbContext.Sales.FirstOrDefaultAsync(x => x.Id == saleId, cancellationToken);
        if (sale is null)
        {
            throw new DomainRuleViolationException("This custom order is no longer available.");
        }

        if (sale.FulfilmentStatus is not { } current)
        {
            throw new DomainRuleViolationException("This Sale is not a custom order routed to fulfilment.");
        }

        sale.FulfilmentStatus = current switch
        {
            DomainFulfilmentStatus.Submitted => DomainFulfilmentStatus.InLab,
            DomainFulfilmentStatus.InLab => DomainFulfilmentStatus.ReadyForPickup,
            DomainFulfilmentStatus.ReadyForPickup => DomainFulfilmentStatus.Fulfilled,
            DomainFulfilmentStatus.Fulfilled => throw new DomainRuleViolationException("This custom order is already Fulfilled."),
            _ => throw new ArgumentOutOfRangeException(nameof(current), current, null),
        };

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Resolves each order's retail point and Retailer through OrgTreeLookup, so this
    /// screen and the Dashboard name the same Retailer for the same retail point: the nearest
    /// Intermediate-level ancestor (CONTEXT.md), not the retail point's immediate parent node,
    /// which the two definitions disagree about whenever a retail point hangs directly off a
    /// Country.
    ///
    /// Fed from IUnscopedReportQueryService, not the plain scoped OrganisationNodes query this
    /// used to run. The old comment argued the scoped query was safe because CustomOrdersView
    /// gates the screen at Country level+, so a caller could never sit below the nodes being
    /// resolved — but that reasons from an RBAC policy to a data-scoping conclusion, which is
    /// exactly the conflation CLAUDE.md's "Data scoping vs RBAC" rule exists to stop: naming an
    /// ancestor is an ancestor lookup whatever level the policy admits today, and the policy is
    /// free to change without anyone noticing this depended on it.</summary>
    private async Task<List<EnrichedOrder>> EnrichAsync(List<Sale> orders, CancellationToken cancellationToken)
    {
        var customerIds = orders.Select(s => s.CustomerId).Distinct().ToList();
        var customers = await dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var orgLookup = new OrgTreeLookup(await unscopedReportQueryService.GetOrganisationNodesUnscopedAsync(cancellationToken));

        return orders.Select(s =>
        {
            var retailer = orgLookup.RowRetailer(s.HierarchyPath);
            var retailPoint = orgLookup.RowOutlet(s.HierarchyPath);
            var customer = customers.GetValueOrDefault(s.CustomerId);
            return new EnrichedOrder(
                s,
                new RetailerKey(retailer.Kind, retailer.Node?.Id ?? Guid.Empty), retailer.Name,
                retailPoint?.Id ?? Guid.Empty, retailPoint?.Name ?? OrgTreeLookup.UnknownOutlet,
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

    /// <summary>What the retailer tier groups on. The Retailer node's Id when there is one; when
    /// there isn't, the resolution kind carries the group instead, because "this retail point sits
    /// directly under a Country and so has no Retailer" and "this path is not a node in the tree
    /// at all" are different facts (CONTEXT.md) and must not collapse into the one Guid.Empty
    /// bucket the old immediate-parent fallback gave them both.</summary>
    private sealed record RetailerKey(RetailerResolutionKind Kind, Guid Id);

    private sealed record EnrichedOrder(Sale Sale, RetailerKey Retailer, string RetailerName, Guid RetailPointId, string RetailPointName, Guid CustomerId, string CustomerName);
}

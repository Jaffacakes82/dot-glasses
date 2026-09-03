using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.CustomOrders;

/// <summary>Read + one write action backing the Admin Portal's Custom Orders screen — the
/// fulfilment-status queue over Sale rows with FulfilmentStatus set (i.e. OrderFromDotGlasses was
/// true at creation). Hierarchy scoping is automatic (Sale/Customer/OrganisationNode all
/// implement IHierarchyScoped), so ListGroupedAsync just needs to query normally — a Country-level
/// caller only ever sees their own subtree's custom orders, matching
/// AuthorizationPolicies.CustomOrdersView's page-level gate (Country level+, so a caller here can
/// never be scoped below the retailer/retail-point nodes it needs to resolve — no
/// IUnscopedReportQueryService ancestor lookup needed, unlike Event History/Dashboard).</summary>
public interface ICustomOrderService
{
    /// <summary>Grouped by retailer -> retail point -> customer name (2026-09-03 — replaces the
    /// former flat paged list; custom-order volume is naturally small, like Organisations/
    /// Reference Data, so no pagination). status narrows which order rows appear under each
    /// customer; the retailer/retail-point ActiveCount badges are computed from the caller's
    /// *entire* scoped order set regardless of status, so they read as a stable "how many active
    /// custom orders sit here" signal rather than shifting with whichever status tab is
    /// selected.</summary>
    Task<CustomOrderGroupedResult> ListGroupedAsync(FulfilmentStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Linear, forward-only: Submitted -> InLab -> ReadyForPickup -> Fulfilled. Throws
    /// if the Sale isn't a custom order (FulfilmentStatus is null) or is already Fulfilled.</summary>
    Task AdvanceStatusAsync(Guid saleId, CancellationToken cancellationToken = default);
}

public record CustomOrderRow(Guid SaleId, string CustomerName, string Outlet, string Prescription, FulfilmentStatus Status, DateTimeOffset CreatedAtUtc);

/// <summary>"Active" = not yet Fulfilled (Submitted/InLab/ReadyForPickup) — see ListGroupedAsync's
/// doc comment for why this count ignores the current status filter. Grouped by RetailerId, not
/// name — OrganisationNode names aren't guaranteed unique, so grouping by name alone could merge
/// two distinct nodes that happen to share a display name. RetailerId is Guid.Empty when no
/// resolvable parent node exists (falls back to one shared "Unknown retailer" bucket).</summary>
public record RetailerOrderGroup(Guid RetailerId, string RetailerName, int ActiveCount, IReadOnlyList<RetailPointOrderGroup> RetailPoints);

/// <summary>RetailPointName is whatever node is the immediate parent of the order's own
/// RetailPoint node — normally an Intermediate reseller, but could itself be the Country node if
/// no reseller tier exists between them. Nested multi-level reseller chains collapsing to one
/// "retailer" tier, and the retailer-vs-retail-point grouping toggle, are both explicitly Day 2
/// per the ticket. Grouped by RetailPointId for the same reason as RetailerOrderGroup.</summary>
public record RetailPointOrderGroup(Guid RetailPointId, string RetailPointName, int ActiveCount, IReadOnlyList<CustomerOrderGroup> Customers);

/// <summary>Grouped by CustomerId, not name — two distinct Customer rows can share a display
/// name (matching is "exact name + phone, no fuzzy matching" per CLAUDE.md), so grouping by name
/// alone could silently merge two different people's orders under one card.</summary>
public record CustomerOrderGroup(Guid CustomerId, string CustomerName, IReadOnlyList<CustomOrderRow> Orders);

public record CustomOrderGroupedResult(IReadOnlyList<RetailerOrderGroup> Retailers, int TotalCount);

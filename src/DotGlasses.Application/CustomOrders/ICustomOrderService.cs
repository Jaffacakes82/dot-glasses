using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.CustomOrders;

/// <summary>Read + one write action backing the Admin Portal's Custom Orders screen — the
/// fulfilment-status queue over Sale rows with FulfilmentStatus set (i.e. OrderFromDotGlasses was
/// true at creation). Hierarchy scoping is automatic (Sale/Customer/OrganisationNode all
/// implement IHierarchyScoped), so ListGroupedAsync just needs to query normally — a Country-level
/// caller only ever sees their own subtree's custom orders, matching
/// AuthorizationPolicies.CustomOrdersView's page-level gate.
///
/// Which orders are *visible* is that scoping. Naming the Retailer or retail point above one is a
/// separate question — an ancestor lookup — and so goes through IUnscopedReportQueryService and
/// OrgTreeLookup exactly as Event History and the Dashboard do. The level a policy happens to
/// admit is not an answer to the second question (CLAUDE.md, "Data scoping vs RBAC — do not
/// conflate").</summary>
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

    /// <summary>Export variant of ListGroupedAsync — same status filter and scoping, unpaged and
    /// flat (every matching row, not grouped/paged), so the CSV export drives off the same
    /// filtered query the on-screen list uses.</summary>
    Task<IReadOnlyList<CustomOrderRow>> ExportAsync(FulfilmentStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Linear, forward-only: Submitted -> InLab -> ReadyForPickup -> Fulfilled. Throws
    /// DomainRuleViolationException carrying user-facing copy if the Sale isn't visible to the
    /// caller, isn't a custom order (FulfilmentStatus is null), or is already Fulfilled — the last
    /// is the live case, since a shared fulfilment queue means a colleague, a double click or a
    /// browser resubmit can all advance the same order twice. See the implementation's doc comment
    /// for why the not-visible case is a rejection rather than a leaked missing row.</summary>
    Task AdvanceStatusAsync(Guid saleId, CancellationToken cancellationToken = default);
}

/// <summary>ConsentGiven is carried through even though the on-screen Custom Orders list doesn't
/// display it — the export must include it wherever a row derives from lead/customer data (this
/// one does, via CustomerName), per the binding requirement in docs/open-issues.md.</summary>
public record CustomOrderRow(Guid SaleId, string CustomerName, string Outlet, string Prescription, FulfilmentStatus Status, DateTimeOffset CreatedAtUtc, bool ConsentGiven);

/// <summary>"Active" = not yet Fulfilled (Submitted/InLab/ReadyForPickup) — see ListGroupedAsync's
/// doc comment for why this count ignores the current status filter. One group per Retailer, where
/// Retailer is CONTEXT.md's definition and no other: the nearest Intermediate-level ancestor of the
/// order's retail point, resolved by OrgTreeLookup. Grouped by identity, not name —
/// OrganisationNode names aren't guaranteed unique, so grouping by name alone could merge two
/// distinct nodes that happen to share a display name.
///
/// Two groups carry no Retailer node and so report RetailerId = Guid.Empty. They are still
/// *separate* groups, told apart by RetailerName: "No retailer" (the retail point hangs directly
/// off a Country, so it genuinely has none — the screen says so rather than substituting the
/// country) and "Unknown retailer" (the order's hierarchy path names no node in the tree at all —
/// a data problem). Before 2026-09-05 a Retailer here was the retail point's immediate parent
/// node, which reported the country as the retailer in the first case and gave both cases one
/// shared "Unknown retailer" bucket.</summary>
public record RetailerOrderGroup(Guid RetailerId, string RetailerName, int ActiveCount, IReadOnlyList<RetailPointOrderGroup> RetailPoints);

/// <summary>RetailPointName is the order's own retail point — the node its hierarchy path sits on
/// exactly, "Unknown outlet" when that path names no node. Nested multi-level reseller chains
/// collapsing to one "retailer" tier, and the retailer-vs-retail-point grouping toggle, are both
/// explicitly Day 2 per the ticket. Grouped by RetailPointId for the same reason as
/// RetailerOrderGroup.</summary>
public record RetailPointOrderGroup(Guid RetailPointId, string RetailPointName, int ActiveCount, IReadOnlyList<CustomerOrderGroup> Customers);

/// <summary>Grouped by CustomerId, not name — two distinct Customer rows can share a display
/// name (matching is "exact name + phone, no fuzzy matching" per CLAUDE.md), so grouping by name
/// alone could silently merge two different people's orders under one card.</summary>
public record CustomerOrderGroup(Guid CustomerId, string CustomerName, IReadOnlyList<CustomOrderRow> Orders);

public record CustomOrderGroupedResult(IReadOnlyList<RetailerOrderGroup> Retailers, int TotalCount);

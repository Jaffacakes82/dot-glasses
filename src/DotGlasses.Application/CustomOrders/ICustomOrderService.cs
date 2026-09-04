using DotGlasses.Application.Reporting;
using DotGlasses.Domain.Enums;

namespace DotGlasses.Application.CustomOrders;

/// <summary>Read + one write action backing the Admin Portal's Custom Orders screen — the flat
/// fulfilment-status queue over Sale rows with FulfilmentStatus set (i.e. OrderFromDotGlasses was
/// true at creation). Hierarchy scoping is automatic (Sale/Customer/OrganisationNode all
/// implement IHierarchyScoped), so ListAsync just needs to query normally — a Country-level
/// caller only ever sees their own subtree's custom orders, matching
/// AuthorizationPolicies.CustomOrdersView's page-level gate.</summary>
public interface ICustomOrderService
{
    /// <summary>status filters to an exact FulfilmentStatus; null returns every status. Filters
    /// before paging (a DB-level Where, not an in-memory filter after the page is loaded).</summary>
    Task<PagedResult<CustomOrderRow>> ListAsync(FulfilmentStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Export variant of ListAsync — same status filter and scoping, unpaged (every
    /// matching row, not one page), so the CSV export drives off the same filtered query the
    /// on-screen list uses.</summary>
    Task<IReadOnlyList<CustomOrderRow>> ExportAsync(FulfilmentStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Linear, forward-only: Submitted -> InLab -> ReadyForPickup -> Fulfilled. Throws
    /// if the Sale isn't a custom order (FulfilmentStatus is null) or is already Fulfilled.</summary>
    Task AdvanceStatusAsync(Guid saleId, CancellationToken cancellationToken = default);
}

/// <summary>ConsentGiven is carried through even though the on-screen Custom Orders list doesn't
/// display it — the export must include it wherever a row derives from lead/customer data (this
/// one does, via CustomerName), per the binding requirement in docs/open-issues.md.</summary>
public record CustomOrderRow(Guid SaleId, string CustomerName, string Outlet, string Prescription, FulfilmentStatus Status, DateTimeOffset CreatedAtUtc, bool ConsentGiven);

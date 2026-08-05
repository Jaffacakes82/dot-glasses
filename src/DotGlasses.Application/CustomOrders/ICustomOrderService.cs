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
    Task<IReadOnlyList<CustomOrderRow>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Linear, forward-only: Submitted -> InLab -> ReadyForPickup -> Fulfilled. Throws
    /// if the Sale isn't a custom order (FulfilmentStatus is null) or is already Fulfilled.</summary>
    Task AdvanceStatusAsync(Guid saleId, CancellationToken cancellationToken = default);
}

public record CustomOrderRow(Guid SaleId, string CustomerName, string Outlet, string Prescription, FulfilmentStatus Status, DateTimeOffset CreatedAtUtc);

using DotGlasses.Domain.Enums;

namespace DotGlasses.Web.Models;

public record CustomOrder(Guid SaleId, string Customer, string Outlet, string Prescription, FulfilmentStatus Status)
{
    public static readonly IReadOnlyDictionary<FulfilmentStatus, string> StatusLabel = new Dictionary<FulfilmentStatus, string>
    {
        [FulfilmentStatus.Submitted] = "Submitted",
        [FulfilmentStatus.InLab] = "In Lab",
        [FulfilmentStatus.ReadyForPickup] = "Ready for Pickup",
        [FulfilmentStatus.Fulfilled] = "Fulfilled",
    };

    public static readonly IReadOnlyDictionary<FulfilmentStatus, string> StatusColor = new Dictionary<FulfilmentStatus, string>
    {
        [FulfilmentStatus.Submitted] = "var(--dot-yellow)",
        [FulfilmentStatus.InLab] = "var(--dot-blue)",
        [FulfilmentStatus.ReadyForPickup] = "var(--dot-pink)",
        [FulfilmentStatus.Fulfilled] = "var(--dot-green)",
    };

    private static readonly FulfilmentStatus[] Flow =
    [
        FulfilmentStatus.Submitted, FulfilmentStatus.InLab, FulfilmentStatus.ReadyForPickup, FulfilmentStatus.Fulfilled,
    ];

    public FulfilmentStatus? NextStatus => Flow.SkipWhile(s => s != Status).Skip(1).Cast<FulfilmentStatus?>().FirstOrDefault();
}

/// <summary>Retailer -> retail point -> customer name, in that order (2026-09-03) — see
/// ICustomOrderService.ListGroupedAsync's doc comment for how ActiveCount is computed and why it
/// ignores the current Status filter.</summary>
public record RetailerGroup(string RetailerName, int ActiveCount, IReadOnlyList<RetailPointGroup> RetailPoints);
public record RetailPointGroup(string RetailPointName, int ActiveCount, IReadOnlyList<CustomerGroup> Customers);
public record CustomerGroup(string CustomerName, IReadOnlyList<CustomOrder> Orders);

public class CustomOrdersViewModel
{
    public required IReadOnlyList<RetailerGroup> Retailers { get; init; }
    public FulfilmentStatus? Status { get; init; }
    public int TotalCount { get; init; }
}

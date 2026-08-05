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

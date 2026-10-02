namespace DotGlasses.Contracts.Common;

/// <summary>How far a custom order has got — mirrors DotGlasses.Domain.Enums.FulfilmentStatus (see
/// Contracts.Common.Gender for why Contracts keeps its own copy). Forward-only.</summary>
public enum CustomOrderStatus
{
    Submitted = 0,
    InLab = 1,
    ReadyForPickup = 2,
    Fulfilled = 3,
}

public static class CustomOrderStatusLabels
{
    /// <summary>The wording the Custom Orders screen uses, so the Field App's Leads list and the
    /// conversion forms say the same thing.</summary>
    public static string Label(this CustomOrderStatus status) => status switch
    {
        CustomOrderStatus.Submitted => "Submitted",
        CustomOrderStatus.InLab => "In Lab",
        CustomOrderStatus.ReadyForPickup => "Ready for Pickup",
        CustomOrderStatus.Fulfilled => "Fulfilled",
        _ => status.ToString(),
    };
}

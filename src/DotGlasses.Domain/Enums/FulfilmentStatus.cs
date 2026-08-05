namespace DotGlasses.Domain.Enums;

/// <summary>Linear, forward-only progression for a custom order routed to fulfilment
/// (Sale.OrderFromDotGlasses) — see Sale.FulfilmentStatus.</summary>
public enum FulfilmentStatus
{
    Submitted = 0,
    InLab = 1,
    ReadyForPickup = 2,
    Fulfilled = 3,
}

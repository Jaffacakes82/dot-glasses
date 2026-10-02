namespace DotGlasses.Domain.Enums;

/// <summary>Linear, forward-only progression of a custom order through the lab — see
/// CustomOrder.Status (ADR-0008).</summary>
public enum FulfilmentStatus
{
    Submitted = 0,
    InLab = 1,
    ReadyForPickup = 2,
    Fulfilled = 3,
}

namespace DotGlasses.Domain.Enums;

/// <summary>
/// Picked per-transaction by the technician (Lead/Sale), not locked at the org level — replaces
/// the earlier "Classical Optician" org-level flag concept. Custom means full e-commerce-
/// equivalent spec (sphere/cylinder/axis/add power per eye), not a PresetCatalogue selection.
/// </summary>
public enum LensRangeType
{
    SixLensSet = 0,
    NineLensSet = 1,
    Custom = 2,
}

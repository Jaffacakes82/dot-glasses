namespace DotGlasses.Domain.Enums;

/// <summary>
/// Picked per-transaction by the technician, not locked at the org level. LensSet means one of the
/// lens sets reaching the retail point — which one is PresetCatalogueId, never this value
/// (ADR-0005). Custom means full e-commerce-equivalent spec (sphere/cylinder/axis/add power per
/// eye), not a lens set.
///
/// Custom keeps its old value 2 on purpose: it was 2 when 6-Lens/9-Lens were separate members, so
/// a stored or queued Custom still means Custom. The gap at 1 is where NineLensSet used to be.
/// </summary>
public enum LensRangeType
{
    LensSet = 0,
    Custom = 2,
}

namespace DotGlasses.Domain.Enums;

/// <summary>
/// What role this catalogue plays in the Field App's lens-range picker. Replaces the earlier
/// approach of matching PresetCatalogueDto.Name against "6-Lens"/"9-Lens" substrings (see
/// LensRangeSelector.razor) — at most one catalogue may hold SixLensSet, and at most one may hold
/// NineLensSet (enforced in PresetCatalogueAdminService), so the Field App can resolve each
/// unambiguously. Other means a catalogue with no special picker role — any number may exist.
/// </summary>
public enum PresetCatalogueKind
{
    Other = 0,
    SixLensSet = 1,
    NineLensSet = 2,
}

namespace DotGlasses.Contracts.Common;

/// <summary>Mirrors DotGlasses.Domain.Enums.PresetCatalogueKind — see CLAUDE.md's Contracts rule
/// (DTOs that need an enum define their own copy rather than referencing Domain).</summary>
public enum PresetCatalogueKind
{
    Other = 0,
    SixLensSet = 1,
    NineLensSet = 2,
}

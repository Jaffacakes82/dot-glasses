namespace DotGlasses.Contracts.PresetCatalogues;

/// <summary>A lens set (CONTEXT.md) as the Field App caches it — already narrowed server-side to
/// the lens sets reaching the caller's retail point.</summary>
public class PresetCatalogueDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<LensOptionDto> LensOptions { get; set; } = [];
}

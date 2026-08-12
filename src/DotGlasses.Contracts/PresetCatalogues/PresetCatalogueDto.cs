using DotGlasses.Contracts.Common;

namespace DotGlasses.Contracts.PresetCatalogues;

public class PresetCatalogueDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PresetCatalogueKind Kind { get; set; }
    public IReadOnlyList<LensOptionDto> LensOptions { get; set; } = [];
}

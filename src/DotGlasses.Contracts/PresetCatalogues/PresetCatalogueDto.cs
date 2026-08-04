namespace DotGlasses.Contracts.PresetCatalogues;

public class PresetCatalogueDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<LensOptionDto> LensOptions { get; set; } = [];
}

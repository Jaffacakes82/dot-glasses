namespace DotGlasses.Contracts.PresetCatalogues;

public class LensOptionDto
{
    public Guid Id { get; set; }
    public decimal SphericalPower { get; set; }
    public bool IsBifocal { get; set; }
    public decimal? AddPower { get; set; }
    public Guid CoatingId { get; set; }
    public int SortOrder { get; set; }
}

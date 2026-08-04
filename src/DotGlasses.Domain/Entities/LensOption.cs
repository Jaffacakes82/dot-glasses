namespace DotGlasses.Domain.Entities;

/// <summary>
/// One selectable lens line item within a PresetCatalogue (e.g. "+2.50" in the 6-Lens Set, or the
/// "0.00/+2.50" bifocal). Belongs to exactly one catalogue rather than a reusable many-to-many
/// pool — matches the admin mental model of "add a lens option to this catalogue with its powers
/// and forced coating".
/// </summary>
public class LensOption
{
    public Guid Id { get; set; }

    public Guid PresetCatalogueId { get; set; }

    public decimal SphericalPower { get; set; }

    public bool IsBifocal { get; set; }

    /// <summary>Near-vision add power — set only when IsBifocal.</summary>
    public decimal? AddPower { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = Coating) — the forced default coating for
    /// this lens, e.g. every bifocal option is pinned to Photochromic.</summary>
    public Guid CoatingId { get; set; }

    public int SortOrder { get; set; }
}

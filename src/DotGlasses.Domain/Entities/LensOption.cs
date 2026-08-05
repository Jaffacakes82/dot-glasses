namespace DotGlasses.Domain.Entities;

/// <summary>
/// One selectable lens line item within a PresetCatalogue — "this catalogue includes this
/// lens strength, at this sort order." The actual power/bifocal-ness lives in the referenced
/// ReferenceDataItem's own Label (Category = LensStrength), not duplicated as typed columns here
/// (2026-08-05 rework — previously carried SphericalPower/IsBifocal/AddPower/CoatingId directly;
/// see CLAUDE.md's Admin Portal wiring (Preset Catalogues screen) section for why). Which
/// coatings a chosen LensOption can take is resolved via LensStrengthCoatingOption against
/// LensStrengthRefId, not stored per-LensOption either — a lens strength's available coatings
/// are a property of the reference item, shared across every catalogue that includes it.
/// </summary>
public class LensOption
{
    public Guid Id { get; set; }

    public Guid PresetCatalogueId { get; set; }

    /// <summary>FK to ReferenceDataItem (Category = LensStrength).</summary>
    public Guid LensStrengthRefId { get; set; }

    public int SortOrder { get; set; }
}

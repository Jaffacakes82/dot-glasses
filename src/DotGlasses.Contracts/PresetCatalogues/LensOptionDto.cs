namespace DotGlasses.Contracts.PresetCatalogues;

public class LensOptionDto
{
    public Guid Id { get; set; }

    /// <summary>Resolved from the linked LensStrength reference item — the Field App renders
    /// this directly, no client-side formatting.</summary>
    public string Label { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>Which Coating reference items this specific lens strength is available in —
    /// drives the Field App's coating picker. Empty means not configured yet (a real interim
    /// state for most non-bifocal strengths — see CLAUDE.md).</summary>
    public IReadOnlyList<Guid> AvailableCoatingIds { get; set; } = [];
}

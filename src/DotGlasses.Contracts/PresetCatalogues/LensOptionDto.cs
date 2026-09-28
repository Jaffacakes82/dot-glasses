namespace DotGlasses.Contracts.PresetCatalogues;

/// <summary>
/// One lens in a lens set, as the Field App caches it (ADR-0007): a <b>lens power</b> — sphere,
/// cylinder, axis and add, with the same types and meaning as a record's per-eye fields — plus the
/// label technicians choose it by, a lens type when it has an add, the coatings it comes in, and
/// its own coating pairings.
///
/// Every collection defaults to empty, so a payload cached before this shape existed (which
/// carried <c>SortOrder</c> and <c>AvailableCoatingIds</c> instead) still deserializes: the unknown
/// names are ignored and the lens simply offers no coatings until the next online load refreshes
/// the cache.
/// </summary>
public class LensOptionDto
{
    public Guid Id { get; set; }

    /// <summary>Typed by the admin — the Field App renders it as-is.</summary>
    public string Label { get; set; } = string.Empty;

    public decimal Sphere { get; set; }

    /// <summary>Blank means 0.00.</summary>
    public decimal? Cylinder { get; set; }

    /// <summary>Only with a cylinder.</summary>
    public decimal? Axis { get; set; }

    /// <summary>Blank or 0.00 means no add.</summary>
    public decimal? Add { get; set; }

    /// <summary>A LensType reference item when the lens has an add; null means single
    /// vision.</summary>
    public Guid? LensTypeRefId { get; set; }

    public string? LensTypeOtherText { get; set; }

    /// <summary>The Coating reference items this lens comes in.</summary>
    public IReadOnlyList<Guid> CoatingIds { get; set; } = [];

    /// <summary>This lens's own coating pairings — choosing the trigger brings the paired coating
    /// with it. Global pairings no longer exist (ADR-0007, "Coatings").</summary>
    public IReadOnlyList<LensCoatingPairingDto> Pairings { get; set; } = [];
}

/// <summary>Directional: on this lens, TriggerCoatingRefId brings PairedCoatingRefId with it.</summary>
public class LensCoatingPairingDto
{
    public Guid TriggerCoatingRefId { get; set; }
    public Guid PairedCoatingRefId { get; set; }
}

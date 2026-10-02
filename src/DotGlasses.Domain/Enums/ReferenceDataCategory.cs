namespace DotGlasses.Domain.Enums;

/// <summary>
/// One generic ReferenceDataItem table backs every admin-managed dropdown list rather than six
/// near-identical entities. Category correctness on FKs (e.g. LensOptionCoating.CoatingRefId must
/// point at a Coating row) is enforced in the Application layer, not the database — the standard
/// trade-off generic reference tables make.
///
/// <b>6 is retired, not free.</b> It was "Lens strength" — curated labels a lens set lens pointed
/// at — until ADR-0007 made a lens set lens a lens power (2026-09-28). The value is never reused:
/// the column is a plain integer, so a reused number would silently give any stray row or cached
/// payload that still says 6 a new meaning. The next new category takes 9.
/// </summary>
public enum ReferenceDataCategory
{
    Occupation = 0,
    ReasonNotPurchased = 1,
    ReferralReason = 2,
    Coating = 3,
    FrameColour = 4,
    HardCaseColour = 5,

    // 6 — retired ("Lens strength", ADR-0007). Reserved; do not reuse.

    /// <summary>Bifocal/Progressive/Other — asked when a lens has an add above 0.00, on a custom
    /// prescription and on a lens set lens alike; single vision is inferred and is not an item
    /// here (ADR-0007).</summary>
    LensType = 7,

    /// <summary>The colours children's frames come in — their own list with their own pictures.
    /// <see cref="FrameColour"/> (4) is the adult list. A Sale's colour comes from the list that
    /// matches its "children's frame" tick; Sales recorded before the split keep the id they
    /// have.</summary>
    FrameColourChild = 8,
}

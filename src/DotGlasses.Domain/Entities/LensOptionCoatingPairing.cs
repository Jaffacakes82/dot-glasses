namespace DotGlasses.Domain.Entities;

/// <summary>
/// A <b>coating pairing</b> on one lens set lens (ADR-0007, "Coatings"): choosing
/// TriggerCoatingRefId on this lens brings PairedCoatingRefId with it, because that is what DGI
/// manufactures for this lens. Directional, like ADR-0001's pairing, but no longer global — the
/// global CoatingPairing table was removed, and a custom prescription has no pairings. Both ids are
/// Coating reference items, and both should be among the lens's own coatings.
/// </summary>
public class LensOptionCoatingPairing
{
    public Guid Id { get; set; }

    public Guid LensOptionId { get; set; }

    public Guid TriggerCoatingRefId { get; set; }

    public Guid PairedCoatingRefId { get; set; }
}

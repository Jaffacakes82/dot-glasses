namespace DotGlasses.Domain.Entities;

/// <summary>
/// Directional: selecting TriggerCoatingRefId auto-adds PairedCoatingRefId to a lens's coating
/// set. The reverse does not automatically apply — see ADR-0001. Both FKs point at
/// ReferenceDataItem (Category = Coating); category/active correctness is enforced in the
/// Application layer, the same trade-off ReferenceDataItem's own doc comment makes for every
/// other FK into it. Admin-configurable from the Reference Data screen's Coating category, not
/// hardcoded.
/// </summary>
public class CoatingPairing
{
    public Guid Id { get; set; }

    public Guid TriggerCoatingRefId { get; set; }

    public Guid PairedCoatingRefId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

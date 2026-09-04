namespace DotGlasses.Domain.Entities;

/// <summary>
/// Symmetric: CoatingRefIdA and CoatingRefIdB can never both be present in the same lens's
/// coating set — see ADR-0001. One row per unordered pair; CoatingRefIdA/CoatingRefIdB are
/// canonicalized (lower Guid first, by CompareTo) at write time so a pair is never stored twice
/// under swapped order, and every read site normalizes the same way before querying rather than
/// checking both orderings. Both FKs point at ReferenceDataItem (Category = Coating); category/
/// active correctness is enforced in the Application layer, matching CoatingPairing.
/// Admin-configurable from the Reference Data screen's Coating category, not hardcoded.
/// </summary>
public class CoatingExclusion
{
    public Guid Id { get; set; }

    public Guid CoatingRefIdA { get; set; }

    public Guid CoatingRefIdB { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>The single shared canonicalization rule every read/write site uses — see this
    /// type's own doc comment for why canonicalizing (rather than checking both orderings) is the
    /// chosen approach.</summary>
    public static (Guid Lower, Guid Higher) Canonicalize(Guid a, Guid b) =>
        a.CompareTo(b) <= 0 ? (a, b) : (b, a);
}

using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// What the Field App does with a coating selection when the coatings on offer change or one is
/// ticked (ADR-0007, "Coatings"). The offered coatings and the pairings come from Rules
/// (<c>LensSetLenses.CoatingsFor</c>) — this is only the device's handling of them, kept as pure
/// functions so the coating selector and the consultation form can't disagree about it.
///
/// <para>
/// A pairing "Blue block → Photochromic" means Photochromic comes with Blue block on that lens: the
/// server refuses a Sale holding the trigger without its paired coating, so the form ticks the
/// paired coating for the technician and locks it while the trigger is ticked.
/// </para>
/// </summary>
public static class CoatingSelection
{
    /// <summary>A selection after it has been brought in line with what is on offer:
    /// <paramref name="Selected"/> is what remains ticked (paired coatings added), and
    /// <paramref name="Removed"/> is what was unticked because it is no longer offered.</summary>
    public sealed record Reconciled(List<Guid> Selected, List<Guid> Removed);

    /// <summary>
    /// Keeps the ticks that are still offered, unticks the rest (reported in
    /// <see cref="Reconciled.Removed"/> so the technician can be told), then ticks the paired
    /// coating of every ticked trigger — a coating carried over from a Lead's preference, or a
    /// Failed record, may be a trigger whose pair was never ticked.
    /// </summary>
    public static Reconciled Reconcile(
        IEnumerable<Guid> selected, IReadOnlyCollection<Guid> offered, IReadOnlyList<CoatingPairingRule> pairings)
    {
        var kept = new List<Guid>();
        var removed = new List<Guid>();
        foreach (var id in selected.Distinct())
        {
            (offered.Contains(id) ? kept : removed).Add(id);
        }

        return new Reconciled(WithPairedCoatings(kept, offered, pairings), removed);
    }

    /// <summary>
    /// <paramref name="selected"/> plus, repeated until nothing more is added, the paired coating
    /// of each ticked trigger (a chain Clear → Anti-glare → Photochromic ticks both). Only an
    /// offered coating is added — Rules already leaves out any trigger whose pair isn't offered, so
    /// for a ticked (offered) trigger the pair always is.
    /// </summary>
    public static List<Guid> WithPairedCoatings(
        IEnumerable<Guid> selected, IReadOnlyCollection<Guid> offered, IReadOnlyList<CoatingPairingRule> pairings)
    {
        var result = selected.ToList();
        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var pairing in pairings)
            {
                if (result.Contains(pairing.TriggerCoatingRefId)
                    && !result.Contains(pairing.PairedCoatingRefId)
                    && offered.Contains(pairing.PairedCoatingRefId))
                {
                    result.Add(pairing.PairedCoatingRefId);
                    grew = true;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// The ticked trigger that keeps <paramref name="coatingId"/> ticked, or null when nothing does
    /// (so it may be unticked). A pairing that runs both ways (A → B and B → A) never locks: two
    /// coatings each holding the other ticked could never be unticked, so the technician is left
    /// free to untick and the server's message says what's missing.
    /// </summary>
    public static Guid? LockedBy(
        Guid coatingId, IReadOnlyCollection<Guid> selected, IReadOnlyList<CoatingPairingRule> pairings)
    {
        foreach (var pairing in pairings)
        {
            if (pairing.PairedCoatingRefId != coatingId
                || pairing.TriggerCoatingRefId == coatingId
                || !selected.Contains(pairing.TriggerCoatingRefId))
            {
                continue;
            }

            var runsBothWays = pairings.Any(other =>
                other.TriggerCoatingRefId == pairing.PairedCoatingRefId
                && other.PairedCoatingRefId == pairing.TriggerCoatingRefId);
            if (!runsBothWays)
            {
                return pairing.TriggerCoatingRefId;
            }
        }

        return null;
    }
}

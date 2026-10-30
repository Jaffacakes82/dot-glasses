using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.LensSets;

/// <summary>
/// What happens to a coating selection when the coatings on offer change (ADR-0007,
/// "Coatings"). The offered coatings and the pairings come from Rules
/// (<c>LensSetLenses.CoatingsFor</c>), and what a pairing ticks and locks is Rules' too
/// (<see cref="PairedCoatings"/>, shared with the Admin Portal) — this is only the device's
/// handling of a lens change, kept as a pure function so the coating selector and the consultation
/// form can't disagree about it.
/// </summary>
public static class CoatingSelection
{
    /// <summary>A selection after it has been brought in line with what is on offer:
    /// <paramref name="Selected"/> is what remains ticked (paired coatings added), and
    /// <paramref name="Removed"/> is what was unticked because it is no longer offered.</summary>
    public sealed record Reconciled(List<Guid> Selected, List<Guid> Removed);

    /// <summary>
    /// Keeps the ticks that are still offered, unticks the rest (reported in
    /// <see cref="Reconciled.Removed"/> so the technician can be told), ticks the
    /// <see cref="AutoTicked"/> coating when a Coating set is being asked for, then ticks the paired
    /// coating of every ticked trigger — a coating carried over from a Lead's preference, or a
    /// Failed record, may be a trigger whose pair was never ticked.
    ///
    /// <para><paramref name="asksForCoatingSet"/> is the caller's to say: a Sale, and a Lead that
    /// orders its lens, ask for a Coating set; a Coating preference is optional and is never ticked
    /// for anyone, and a set locked from an ordered Lead isn't reconciled at all.</para>
    /// </summary>
    public static Reconciled Reconcile(
        IEnumerable<Guid> selected, IReadOnlyCollection<Guid> offered, IReadOnlyList<CoatingPairingRule> pairings,
        bool asksForCoatingSet)
    {
        var kept = new List<Guid>();
        var removed = new List<Guid>();
        foreach (var id in selected.Distinct())
        {
            (offered.Contains(id) ? kept : removed).Add(id);
        }

        if (asksForCoatingSet && AutoTicked(offered) is { } only && !kept.Contains(only))
        {
            kept.Add(only);
        }

        return new Reconciled(PairedCoatings.WithPairedCoatings(kept, offered, pairings), removed);
    }

    /// <summary>
    /// The coating a Coating set has ticked for it: the only one on offer, since a set needs at
    /// least one coating and that one is the only valid answer. Null when two or more are offered
    /// (the choice is the technician's) or none is. It ticks as the offer changes, not again after
    /// it is unticked: unticking it stays possible and gives the usual "at least one" message.
    /// </summary>
    public static Guid? AutoTicked(IReadOnlyCollection<Guid> offered) =>
        offered.Count == 1 ? offered.First() : null;
}

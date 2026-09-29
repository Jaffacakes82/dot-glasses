using DotGlasses.Rules.LensSets;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.App.ReferenceData;

/// <summary>
/// What the Field App does with a coating selection when the coatings on offer change (ADR-0007,
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

        return new Reconciled(PairedCoatings.WithPairedCoatings(kept, offered, pairings), removed);
    }
}

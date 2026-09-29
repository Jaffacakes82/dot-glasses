using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.LensSets;

/// <summary>
/// What a coating pairing does to a coating selection (ADR-0007, "Coatings") — the one definition
/// the Field App's coating picker and the Admin Portal's lead conversion both apply, so they tick
/// and lock the same coatings. The pairings are a chosen pair's
/// <see cref="LensPairCoatings.RequiredPairings"/>; a Custom prescription has none, and then
/// nothing here does anything.
///
/// <para>
/// A pairing "Blue block → Photochromic" means Photochromic comes with Blue block on that lens:
/// the server refuses a record holding the trigger without its paired coating, so ticking the
/// trigger ticks the paired coating too, and the paired coating is locked while its trigger is
/// ticked. <b>A pairing that runs both ways (A → B and B → A) ticks but never locks</b>: two
/// coatings each holding the other ticked could never be unticked, so the selection is left free
/// and the server's message says what's missing.
/// </para>
/// </summary>
public static class PairedCoatings
{
    /// <summary>
    /// <paramref name="selected"/> plus, repeated until nothing more is added, the paired coating
    /// of each ticked trigger (a chain Clear → Anti-glare → Photochromic ticks both). With
    /// <paramref name="offered"/> given, only an offered coating is added —
    /// <see cref="LensSetLenses.CoatingsFor"/> already leaves out a trigger whose pair isn't
    /// offered, so for a ticked, offered trigger the pair always is. Keeps
    /// <paramref name="selected"/>' order, each addition after it.
    /// </summary>
    public static List<Guid> WithPairedCoatings(
        IEnumerable<Guid> selected, IReadOnlyCollection<Guid>? offered, IReadOnlyList<CoatingPairingRule> pairings)
    {
        var result = selected.Distinct().ToList();
        var grew = true;
        while (grew)
        {
            grew = false;
            foreach (var pairing in pairings)
            {
                if (result.Contains(pairing.TriggerCoatingRefId)
                    && !result.Contains(pairing.PairedCoatingRefId)
                    && (offered is null || offered.Contains(pairing.PairedCoatingRefId)))
                {
                    result.Add(pairing.PairedCoatingRefId);
                    grew = true;
                }
            }
        }

        return result;
    }

    /// <summary>The coatings ticking <paramref name="trigger"/> locks: each one a pairing from it
    /// names, unless that pairing runs both ways (or names the trigger itself).</summary>
    public static IReadOnlyList<Guid> LockedByTicking(Guid trigger, IReadOnlyList<CoatingPairingRule> pairings) =>
        pairings
            .Where(p => p.TriggerCoatingRefId == trigger && p.PairedCoatingRefId != trigger && !RunsBothWays(p, pairings))
            .Select(p => p.PairedCoatingRefId)
            .Distinct()
            .ToList();

    /// <summary>The ticked trigger that keeps <paramref name="coatingId"/> ticked, or null when
    /// nothing does (so it may be unticked).</summary>
    public static Guid? LockedBy(Guid coatingId, IReadOnlyCollection<Guid> selected, IReadOnlyList<CoatingPairingRule> pairings)
    {
        foreach (var pairing in pairings)
        {
            if (pairing.PairedCoatingRefId == coatingId
                && selected.Contains(pairing.TriggerCoatingRefId)
                && LockedByTicking(pairing.TriggerCoatingRefId, pairings).Contains(coatingId))
            {
                return pairing.TriggerCoatingRefId;
            }
        }

        return null;
    }

    private static bool RunsBothWays(CoatingPairingRule pairing, IReadOnlyList<CoatingPairingRule> pairings) =>
        pairings.Any(other =>
            other.TriggerCoatingRefId == pairing.PairedCoatingRefId
            && other.PairedCoatingRefId == pairing.TriggerCoatingRefId);
}

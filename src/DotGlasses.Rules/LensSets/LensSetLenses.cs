using DotGlasses.Contracts.Common;
using DotGlasses.Rules.LensPowers;
using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.LensSets;

/// <summary>
/// How every screen lists, matches and offers coatings for a lens set's lenses (ADR-0007) — one
/// definition shared by the Field App, the Admin Portal and the server, so a lens set looks and
/// behaves the same wherever it is shown. Pure functions over <see cref="LensOptionSnapshot"/>,
/// the lens shape both of the snapshot's fillings already carry (the server's from the database,
/// the device's from the cached lens-set DTOs), so no caller has to convert anything first.
/// </summary>
public static class LensSetLenses
{
    /// <summary>
    /// A lens set's lenses in the fixed display order: single vision (no add) by sphere, then the
    /// lenses with an add grouped by lens type — Bifocal, Progressive, Other — each by add, then by
    /// sphere. Ties (single vision lenses with the same sphere but different cylinders, or a
    /// duplicate a set shouldn't hold) fall back to the label and then the id, so the order never
    /// depends on the order the lenses arrived in.
    ///
    /// <para>
    /// Which lens type comes first is read from the LensType reference list in
    /// <paramref name="referenceItems"/> — its own admin-set order (seeded Bifocal, Progressive,
    /// Other), with the category's "Other" item always last — rather than from hard-coded ids or
    /// labels: Rules can't see the seeded Guids (they live in Infrastructure), the labels are
    /// admin-editable, and the list's order is the one both fillings already carry
    /// (<see cref="ReferenceDataSnapshot.Items"/> is in category/sort order on the server and on the
    /// device). A lens whose lens type isn't in the list — a retired item on the device, whose copy
    /// holds active items only — sorts after Other rather than disappearing.
    /// </para>
    /// </summary>
    public static IReadOnlyList<LensOptionSnapshot> InDisplayOrder(
        IEnumerable<LensOptionSnapshot> lenses, IEnumerable<ReferenceItemSnapshot> referenceItems)
    {
        var lensTypeRank = referenceItems
            .Where(item => item.Category == ReferenceDataCategory.LensType)
            .OrderBy(item => item.IsOtherOption) // stable: keeps the list's own order within each half
            .Select((item, index) => (item.Id, Rank: index))
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Rank);

        // Single vision is group 0; a lens with an add is 1 + its lens type's rank.
        int GroupOf(LensOptionSnapshot lens) =>
            !LensPowerRules.HasAdd(lens.Add) ? 0
            : lens.LensTypeRefId is { } lensType && lensTypeRank.TryGetValue(lensType, out var rank) ? 1 + rank
            : int.MaxValue;

        return lenses
            .OrderBy(GroupOf)
            .ThenBy(lens => LensPowerRules.NormaliseAdd(lens.Add) ?? 0m)
            .ThenBy(lens => lens.Sphere)
            .ThenBy(lens => lens.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(lens => lens.Id)
            .ToList();
    }

    /// <summary>The same order, with the LensType list read off a snapshot — for a caller that
    /// already holds one (the Field App's adapter, the Admin Portal).</summary>
    public static IReadOnlyList<LensOptionSnapshot> InDisplayOrder(
        IEnumerable<LensOptionSnapshot> lenses, ReferenceDataSnapshot referenceData) =>
        InDisplayOrder(lenses, referenceData.Items);

    /// <summary>
    /// The lens in <paramref name="lenses"/> with this lens power and lens type, or null — no match
    /// is a normal answer (the lens has since been removed from the set, or the record never came
    /// from it). Used to seed a Sale from a Lead, to pre-select a Failed record's lens, and by the
    /// consultation rules' lens-set branch. A record holds no pointer to the lens it came from
    /// (ADR-0007); a set may repeat a power only with a different lens type, so power plus lens type
    /// picks out at most one lens.
    ///
    /// <para>
    /// Powers are compared the way <see cref="LensPowerRules"/> reads them, not as stored: an add of
    /// 0.00 is no add (<see cref="LensPowerRules.NormaliseAdd"/>), a blank cylinder is 0.00, and an
    /// axis only counts alongside a cylinder (<see cref="LensPowerRules.HasCylinder"/>) — so a
    /// record that sent 0.00 where the lens holds nothing is still the same lens. The lens type is
    /// compared as given: null is single vision, so a single vision power asked for with a lens
    /// type (or the reverse) is a different lens. A missing sphere matches nothing.
    /// </para>
    /// </summary>
    public static LensOptionSnapshot? Match(
        IEnumerable<LensOptionSnapshot> lenses,
        decimal? sphere, decimal? cylinder, decimal? axis, decimal? add, Guid? lensTypeRefId)
    {
        if (sphere is not { } wantedSphere)
        {
            return null;
        }

        var wanted = Comparable(wantedSphere, cylinder, axis, add);
        return lenses.FirstOrDefault(lens =>
            lens.LensTypeRefId == lensTypeRefId
            && Comparable(lens.Sphere, lens.Cylinder, lens.Axis, lens.Add) == wanted);
    }

    /// <summary>
    /// The coatings a chosen pair of lenses offers, and the pairings that apply to it (ADR-0007,
    /// "Coatings"). A record has one coating set for the pair, so only the coatings
    /// <em>both</em> lenses come in are offered — in <paramref name="left"/>'s order, which both
    /// fillings keep as the Coating list's own order. <em>Both</em> lenses' pairings apply: every
    /// pairing from either lens, each once (left's first). With "Same lens for both eyes", pass the
    /// one lens twice.
    ///
    /// <para>
    /// Deliberately literal: a pairing is returned even when its trigger or its paired coating
    /// isn't offered, and the offered list isn't narrowed to allow for it. What to do with a
    /// trigger whose paired coating one of the lenses doesn't come in is the caller's rule to state
    /// (see <see cref="LensPairCoatings"/>).
    /// </para>
    /// </summary>
    public static LensPairCoatings CoatingsFor(LensOptionSnapshot left, LensOptionSnapshot right) =>
        new(
            left.CoatingIds.Where(right.CoatingIds.Contains).Distinct().ToList(),
            left.Pairings.Concat(right.Pairings).Distinct().ToList());

    /// <summary>One eye's lens power reduced to what makes it the same lens: blank cylinder as
    /// 0.00, no axis without a cylinder, a 0.00 add as none. Decimal equality ignores trailing
    /// zeros, so 2.5 and 2.50 are equal.</summary>
    private static (decimal Sphere, decimal Cylinder, decimal? Axis, decimal? Add) Comparable(
        decimal sphere, decimal? cylinder, decimal? axis, decimal? add) =>
        (sphere,
         cylinder ?? 0m,
         LensPowerRules.HasCylinder(cylinder) ? axis : null,
         LensPowerRules.NormaliseAdd(add));
}

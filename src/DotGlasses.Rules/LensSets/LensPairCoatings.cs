using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.LensSets;

/// <summary>
/// What <see cref="LensSetLenses.CoatingsFor"/> works out for a chosen pair of lens set lenses:
/// <paramref name="Offered"/>, the coatings both lenses come in — the only ones a Sale's Coating
/// set, or a Test or Lead's coating preference, may hold — and <paramref name="RequiredPairings"/>,
/// every pairing from either lens: when a pairing's trigger coating is chosen, its paired coating
/// must be too (the Field App ticks and locks it; the server refuses a record without it).
///
/// Every offered trigger's paired coating is itself offered: a coating whose pairing demands one
/// the pair doesn't offer could never be sold, so <see cref="LensSetLenses.CoatingsFor"/> leaves it
/// out of <paramref name="Offered"/>. <paramref name="RequiredPairings"/> is not narrowed the same
/// way — a pairing whose trigger isn't offered simply never fires.
/// </summary>
public sealed record LensPairCoatings(IReadOnlyList<Guid> Offered, IReadOnlyList<CoatingPairingRule> RequiredPairings);

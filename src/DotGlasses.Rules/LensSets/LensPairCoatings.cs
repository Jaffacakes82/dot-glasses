using DotGlasses.Rules.ReferenceData;

namespace DotGlasses.Rules.LensSets;

/// <summary>
/// What <see cref="LensSetLenses.CoatingsFor"/> works out for a chosen pair of lens set lenses:
/// <paramref name="Offered"/>, the coatings both lenses come in — the only ones a Sale's Coating
/// set, or a Test or Lead's coating preference, may hold — and <paramref name="RequiredPairings"/>,
/// every pairing from either lens: when a pairing's trigger coating is chosen, its paired coating
/// must be too (the Field App ticks and locks it; the server refuses a record without it).
///
/// A pairing's paired coating is not guaranteed to be in <paramref name="Offered"/>: one lens can
/// pair Blue block with Photochromic while the other comes in Blue block but not Photochromic.
/// Choosing Blue block on that pair can then never satisfy both rules.
/// </summary>
public sealed record LensPairCoatings(IReadOnlyList<Guid> Offered, IReadOnlyList<CoatingPairingRule> RequiredPairings);

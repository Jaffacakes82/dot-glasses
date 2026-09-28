# 04 — Rules helpers for lens-set lenses

**What to build:** Three pure helpers in Rules that the Field App, the Admin Portal and the server all
share, so every screen lists, matches and offers coatings the same way:

1. **The fixed display order** of a set's lenses: single vision by sphere, then Bifocal, Progressive
   and Other, each by add, then by sphere.
2. **Matching** a lens power plus lens type to a lens in a set (for conversion seeding and Failed-record
   correction); no match is a normal answer.
3. **Offered coatings and required pairings** for a chosen pair of lenses: the coatings both lenses come
   in, and for every trigger coating from either lens's pairings, the coating it requires.

**Blocked by:** 03 (the lens-set DTO shape)

**Status:** resolved

**Model:** Opus 5.5 — the Rules rework; these define behaviour every client depends on.

**Seam:** `DotGlasses.Rules.Tests`.

**Don't run alongside:** 05 and 06 if they are editing the same Rules files (this ticket should add new
files, not edit `ConsultationRules`).

## Acceptance criteria

- [x] The order helper sorts per the rule above; Rules.Tests pin it with a mixed set.
- [x] The matching helper returns the lens with the same power and lens type, or none. Rules.Tests
      cover an exact match, no match, and two lenses that differ only by lens type.
- [x] The offered-coatings helper returns the intersection of the two lenses' coatings and the
      required pairings from either lens. Rules.Tests cover both.
- [x] Nothing outside Rules re-implements these (later tickets call them).

## Notes

- Spec: `../spec.md` — "New pure helpers in Rules". Rules may reference only Contracts.
- Prior art: `ConsultationRulesTests`, `ReferenceDataSnapshotTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**2026-09-28 — resolved.**

- **Rules API for later tickets** (namespace `DotGlasses.Rules.LensSets`, static class
  `LensSetLenses`). All three take `LensOptionSnapshot` — the lens shape both snapshot fillings
  already carry (server: `ReferenceDataSnapshotProvider`; device: `FromCachedReferenceData` via
  `ReferenceDataSnapshotAdapter`), so the Field App reads a set's lenses with
  `client.ToSnapshot().FindCatalogue(id)!.LensOptions` and needs no conversion.
  - `InDisplayOrder(IEnumerable<LensOptionSnapshot> lenses, IEnumerable<ReferenceItemSnapshot> referenceItems)`
    and `InDisplayOrder(lenses, ReferenceDataSnapshot referenceData)` → `IReadOnlyList<LensOptionSnapshot>`.
    Single vision (`!HasAdd`) by sphere; then lenses with an add grouped by lens type, each by
    add, then sphere; ties by label (ordinal, case-insensitive), then id — independent of input order.
  - `Match(IEnumerable<LensOptionSnapshot> lenses, decimal? sphere, decimal? cylinder, decimal? axis, decimal? add, Guid? lensTypeRefId)`
    → `LensOptionSnapshot?`. Compares through `LensPowerRules`: add via `NormaliseAdd` (0.00 = none),
    blank cylinder = 0.00, axis only counts alongside a cylinder (`HasCylinder`); lens type
    compared as given (null = single vision); null sphere matches nothing. First match in the
    given order. Ticket 07's "power + lens type unique in the set" check is
    `Match(set.Where(l => l.Id != editingId), …) is not null`.
  - `CoatingsFor(LensOptionSnapshot left, LensOptionSnapshot right)` → `LensPairCoatings(IReadOnlyList<Guid> Offered, IReadOnlyList<CoatingPairingRule> RequiredPairings)`.
    `Offered` = coatings both lenses come in, in the left lens's order (the Coating list's order);
    `RequiredPairings` = every pairing from either lens, each once, left's first. "Same lens for
    both eyes" passes the one lens twice.
- **Lens type order — how the types are identified.** From the LensType list's own order in the
  snapshot's `Items` (admin-set `SortOrder`, seeded Bifocal, Progressive, Other), with the
  category's `IsOtherOption` item forced last; a lens type not in the list (retired, on the device,
  whose copy holds active items only) sorts after Other. No hard-coded Guids/labels: Rules can't see
  the seeded ids (Infrastructure), labels are admin-editable, `ReferenceItemSnapshot` carries no
  `Code`, and both fillings already hand over items in category/sort order
  (`ReferenceDataSnapshotProvider` and `ReferenceDataQueryService` both `OrderBy(Category).ThenBy(SortOrder)`).
  Consequence: if an admin reorders the LensType list, the lens order follows it (Other stays last).
- **Interim order replaced.** Its only call site was `ReferenceDataSnapshotProvider` (`ORDER BY add,
  sphere, label`): the query no longer orders, and each set's lenses go through `InDisplayOrder`
  before the snapshot is built. Every server consumer (lens-set API, Lens Sets screen via
  `PresetCatalogueAdminService`, lead conversion) reads lenses off the snapshot, so all inherit it,
  and the Field App receives the API list in that order. No edit to `CataloguesController` or the
  Catalogues views. The example sets' order is unchanged (it coincided with the interim one).
- **Not re-implemented, but still the old meaning (for 06/09/10):** `ConsultationForm.razor`'s
  `AvailableCoatingIdsForSelectedPreset` and `LensRangeSelector`'s no-coatings note read the *left*
  lens's `CoatingIds` only (today's meaning, per ticket 03); ticket 10 should swap them for
  `CoatingsFor(left, right).Offered`, and ticket 06 the server's left-lens check in
  `ConsultationRules`.
- **Open question for 06/10:** `CoatingsFor` is deliberately literal. A pairing's paired coating
  may be outside `Offered` (left pairs Blue block → Photochromic; right comes in Blue block but not
  Photochromic) — choosing Blue block on that pair can then never pass both the "offered" and the
  "pairing" rules. Whether to hide such a trigger from the offered list, or to leave it to the
  admin (ticket 07 could warn), is for 06/10 to decide; the record type's doc says so.
- **Tests:** `tests/DotGlasses.Rules.Tests/LensSets/LensSetLensesTests.cs` (9): the mixed-set
  order, list-order/Other-last/unlisted type, input-order independence; exact match, no match
  (each field, wrong/extra lens type, null sphere, empty set), two lenses differing only by lens
  type, normalisation (0.00 add, 0.00 cylinder, trailing zeros); offered intersection; pairings
  from either lens, deduplicated. Full `dotnet test DotGlasses.sln` — 590 passed, 0 failed
  (Rules 317, Application 96, Infrastructure 62, Web 115); baseline 581.

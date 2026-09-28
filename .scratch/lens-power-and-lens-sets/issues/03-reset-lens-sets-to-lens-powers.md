# 03 — Reset lens sets so each lens is a lens power with its own coatings and pairings

**What to build:** A lens in a lens set stops being a pointer to a "Lens strength" label and becomes a
lens power with a typed label, a lens type when it has an add, the coatings it comes in and its own
coating pairings. Existing lens-set data is reset rather than converted: every existing lens set is
retired and emptied, and Lens strengths, the global coating availability grid and the global Pairings
section disappear (exclusions stay). Old records keep their lens set's name. Local dev and tests get
example 6-Lens and 9-Lens sets in the new shape. The Lens Sets screen and the lens-set API show each
lens's label, power, lens type, coatings and pairings. Coating availability now comes from the lens's
own coatings, with today's meaning (ticket 06 changes the meaning).

**Blocked by:** 02 (shared Rules files), multi-org-access-and-retail-points 04 (the Lens Sets
controller)

**Status:** ready-for-agent

**Model:** Opus 5.5 — the reset migration has a seed-data trap, and it removes three concepts across
every layer.

**Seam:** `DotGlasses.Infrastructure.Tests` for the migration; `DotGlasses.Web.Tests` for the Lens
Sets screen, Reference Data screen and lens-set API; `DotGlasses.Rules.Tests` for coating availability.

**Don't run alongside:** any other migration-bearing ticket; 07 and 08 (Lens Sets controller and view).

## Acceptance criteria

- [ ] A lens-set lens holds label, sphere, cylinder, axis, add and lens type reference, plus two new
      child tables: its coatings, and its pairings (lens, trigger coating, paired coating). It drops
      the Lens strength reference and sort order.
- [ ] `LensStrengthCoatingOption`, global `CoatingPairing` and the `LensStrength` reference category
      are removed. The enum's numeric value is retired, not reused. Coating exclusions are unchanged.
- [ ] One migration resets the data in the same change as the schema: every existing lens set is
      retired (`IsDeleted`, `DeletedAtUtc`); their lenses and assignments are deleted; Lens strength
      items, the grid and global pairings are deleted; exclusions kept. **Edit the generated
      migration** so the two seeded lens sets are retired, not deleted (removing their `HasData`
      makes EF emit `DeleteData`).
- [ ] Old lens-set records keep `LensRangeType` and `PresetCatalogueId` and show their set's name.
      Custom records keep their values, even outside the new ranges.
- [ ] The dev-only seeder and the test fixtures create example 6-Lens and 9-Lens sets in the new
      shape: real powers, labels, coatings, one example pairing, the same assignments as today.
      Staging and production get no active lens sets.
- [ ] The lens-set DTO carries per lens: id, label, sphere, cylinder, axis, add, lens type id, coating
      ids, and pairings as trigger/paired id pairs. Global pairings leave the reference-data payload.
- [ ] Admin Portal: the coating availability grid and its save action are gone; the old add-a-lens-
      strength flow is removed (ticket 07 adds the new dialog); each set's lenses show in a read-only
      table (label, lens power, lens type, coatings, pairings). Reference Data no longer shows Lens
      strengths or the Pairings section; exclusions stay.
- [ ] Coating availability is read from the lens's own coatings (like-for-like with today's
      left-lens check); global pairing enforcement is removed (ticket 06 adds per-lens pairings).
- [ ] Infrastructure.Tests: existing sets end retired and empty; Lens strength items, grid and global
      pairings gone; exclusions intact; an old lens-set record still resolves its set's name; an old
      custom record keeps out-of-range values. Web.Tests: the lens-set API returns the new shape;
      Reference Data shows neither Lens strengths nor Pairings.

## Notes

- Spec: `../spec.md` — "Domain and schema", "Admin Portal" (grid removal, Reference Data), user
  stories 19–20, 41–42. Decision: ADR-0007 including "Coatings". Map ticket 09 for why reset.
- Prior art: `LensRangeMigrationTests`, `PresetCatalogueSnapshotTests`, `LensSetAvailabilityApiTests`,
  `DevUserSeederTests`.
- EF gotchas (handoff) apply. Migrations are applied only by CI.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

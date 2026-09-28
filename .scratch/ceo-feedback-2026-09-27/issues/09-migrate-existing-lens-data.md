# 09 — What happens to existing lens data

Type: grilling
Status: resolved
Blocked by: 06, 07

## Question

Given the product isn't live, what happens to existing lens sets, `LensStrength` reference items,
the coating grid, and nonprod Tests/Leads/Sales that reference them: migrate, reseed or discard?

## Context

- Records reference `LensOption` Ids and `LensStrength` item Ids today.
- Precedent: ticket 04 of triage-2026-09-26 took "no compatibility aliases, not live" (Q5).
- From ticket 06 (ADR-0007):
  - Records will store each eye's lens power, one lens type and the lens range, with no pointer to
    a lens-set entry.
  - `LensOption` entries become powers with typed labels.
  - The `LensStrength` reference category is retired.
  - Today's lens-set labels (e.g. "+0.00 / +2.50 (Bifocal)") would have to be parsed into powers,
    or re-entered by hand.
- From ticket 07 (ADR-0007, "Coatings"): global `CoatingPairing` rows and the
  `LensStrengthCoatingOption` grid are replaced by per-entry coatings and pairings. Exclusions are
  kept unchanged.

## Answer

Grilled 2026-09-28.

What exists today: two seeded lens sets, "6-Lens Set" with 8 entries and "9-Lens Set" with 12,
built from 16 seeded Lens strength items. Twelve are single-vision spheres and four are bifocals.
Only the four bifocals have seeded coatings (Photochromic), so the single-vision entries couldn't
be saved under ticket 07's "at least one coating" rule. Staging may also hold data admins created,
which wasn't inspected.

- **Reset, don't convert.** The migration makes these changes:
  - It retires every existing lens set and empties it, removing its entries and its assignments.
  - It removes the Lens strength reference items and the Lens strength category.
  - It removes the `LensStrengthCoatingOption` grid and the global coating pairings.
  - Exclusions stay.
  - Nothing is parsed or converted. The product isn't live, and staging records are test data.
    Converting would mean parsing free-text labels for sets that can't be sold as they stand. (Q1)
- **Old sets are retired, not deleted,** so existing records still show the set's name. An empty
  set is never offered in the Field App. Names only need to be unique among *active* sets, so
  "6-Lens Set" can be created again straight away. (Q4)
- **Old lens-set records keep their lens range and set name.** Their lens power columns show "—".
  (Q5)
- **Old custom-prescription records stay exactly as recorded,** including values outside the new
  ranges. The new rules apply only to new records. (Q3)
- **Staging and production start with no active lens sets.** DGI builds them on the new screen.
  (Q1)
- **The fixed seed data for lens sets, entries, lens strengths and the grid is removed.** The
  dev-only seeder creates example 6-Lens and 9-Lens sets in the new shape instead: real powers,
  labels, coatings and the same assignments as today. This applies to local dev and tests only.
  (Q2)
- **A lens-set record still queued in the old shape on a device is rejected at sync** and lands on
  Failed records. Cached lens sets refresh on the device's next online load. (Q6)

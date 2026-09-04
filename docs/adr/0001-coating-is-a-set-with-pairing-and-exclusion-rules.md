# Coating is a set, not a single value, with pairing and exclusion rules between coatings

A Sale/Lead's lens previously carried a single `CoatingRefId`. 2026-08-10 stakeholder feedback
requires combinable coatings (e.g. Blue Block + Photochromic together) plus rules governing which
combinations are allowed: **coating pairing** (selecting one coating auto-adds another,
directional — Blue Block → Photochromic, not the reverse) and **coating exclusion** (two
coatings can never coexist in the same set, symmetric — e.g. Clear excludes Photochromic and
Sunglasses). We chose to model Coating as a set of `ReferenceDataItem` references per lens
rather than a single FK, with pairing/exclusion enforced live during selection (pairing adds a
default the technician can still uncheck; a selection that would violate an exclusion is blocked
with an explanation, never silently resolved) and validated for internal consistency at
rule-authoring time — an admin cannot save a pairing that contradicts an existing exclusion, or
vice versa. These rules apply universally (preset and custom lenses alike) — they describe
physical compatibility between coatings, a separate axis from `LensStrengthCoatingOption`'s
existing per-catalogue availability restriction, which stays preset-only and unchanged.

**Considered and rejected**: minting a combined reference-data item per pairing (e.g. a single
"Blue Block + Photochromic" coating option) instead of true multi-select. Rejected because the
number of combined items grows combinatorially as more pairings are added over time (the
feedback explicitly anticipates more), and a combined item can't represent an independent
selection that happens to not be paired (e.g. Photochromic alone).

## Where pairing/exclusion rules live

Admin-configurable data, not hardcoded application logic — consistent with every other
admin-configurable relationship between reference items in this codebase (`LensStrengthCoatingOption`
is the direct precedent), and matching the reviewer's own signal that more pairings are expected
over time. Hardcoding would mean a dev/deploy cycle for what's fundamentally a content decision.

Unlike `LensStrengthCoatingOption` — which relates *two different* reference-data categories
(LensStrength × Coating) and is genuinely about catalogue configuration, so its own doc comment
places it on the Preset Catalogues screen rather than Reference Data — coating pairing/exclusion
relates Coating to Coating within a single category, and describes a property of the coatings
themselves, independent of any catalogue. It's therefore managed from the Reference Data screen's
existing Coating category (editing a Coating item lets an admin set what it pairs with/excludes
among other active Coatings), not a separate admin surface.

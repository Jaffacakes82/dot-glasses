# 07 — Which coatings each lens in a lens set can have, and where pairings live

Type: grilling
Status: open
Blocked by: 06

## Question

When a lens is added to a lens set, the admin chooses the coatings (and lens types) it comes in,
and the pairings that apply to it. Exclusions stay global. What is the model, and what happens to
the global "Lens strength coating availability" grid and to the global pairings?

## Context

- Feedback doc: "Remove section 'Lens strength coating availability'"; "Move pairings to the preset
  catalogue set up stage ... each lens can have different pairings"; exclusions "should be general".
- Call: in a lens set, pairings reflect what Dot Glasses manufactures ("we only manufacture blue
  blocks with photochromic"); on a custom order anything may be paired except the exclusions.
- Call: coatings offered for a bifocal set appeared for lenses that don't come in them — the
  per-lens choice is meant to fix that.
- Today: `LensStrengthCoatingOption` (global grid), `CoatingPairing`/`CoatingExclusion` (global,
  on Reference Data's Coatings & tints).
- From ticket 06 (ADR-0007):
  - A lens-set entry is a lens power, a typed label, a lens type (when it has an add) and the
    coatings it comes in.
  - The admin chooses those coatings at the moment the lens is added to the set.
  - The "Lens strength" reference data is gone, so the global grid has nothing left to key on.

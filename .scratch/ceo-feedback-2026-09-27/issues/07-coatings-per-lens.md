# 07 — Which coatings each lens in a lens set can have, and where pairings live

Type: grilling
Status: resolved
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

## Answer

Grilled 2026-09-28. Recorded as the "Coatings" paragraph in
[ADR-0007](../../../docs/adr/0007-a-lens-power-is-a-value.md), with a note added to ADR-0001.
`CONTEXT.md`'s **Coating set**, **Coating pairing** and **Coating exclusion** are updated.

**Lens sets**
- **Each entry lists the coatings it comes in, and at least one is required.** The admin ticks them
  when adding the lens. The "Lens strength coating availability" grid and the "no coatings
  configured" error both go. (Q5)
- **Each entry can have its own pairings**, for example "Blue block → Photochromic" for this lens
  only. The technician still sees checkboxes; ticking the trigger ticks its partner. The rejected
  alternative was listing exact combinations for each entry, a single-choice list the admin would
  type out lens by lens. (Q2)
- **Saving a pairing that contradicts an exclusion is refused.** An entry may still list two
  excluded coatings as separate options; they just can't both be picked. (Q4)

**Recording**
- **There is one coating set for the pair.** On a lens set, only coatings that both chosen lenses
  come in are offered. This replaces today's check against the left lens only. (Q1)
- **Both lenses' pairings apply to the pair.** (Q9)
- **The server enforces pairings on lens-set records.** A trigger coating without its partner is
  refused, with a message such as "Blue block on this lens only comes with Photochromic." On a lens
  set the partner therefore can't be unticked, whereas ADR-0001 let the technician remove it. (Q6)
- **A Test or Lead's coating preference follows the same list** when both lenses come from a lens
  set. Otherwise any active coating is allowed. (Q7)
- **Custom prescriptions have no pairings.** Any active coatings are allowed, subject to
  exclusions. (Q3)

**Global rules**
- **Global pairings are removed,** along with the Pairings section of Reference Data's Coatings &
  tints. (Q3)
- **Exclusions stay global.** They apply to lens sets and custom prescriptions alike. (Q4)
- **The coating list and how Clear behaves stay admin data.** The handover asks DGI to review the
  list against the shop's: Yellow tint, Polarized and Sunglasses, and exclusions between Clear and
  every other coating if they want the shop's "Clear means no coatings". (Q8)

**Handed on**
- Ticket 08: ticking coatings and adding pairings when a lens is added, and the Q4 refusal.
- Ticket 09: what happens to today's global pairings and the `LensStrengthCoatingOption` grid.
- Ticket 10: offering the coatings both lenses come in, auto-adding from both lenses' pairings, and
  not letting the paired coating be unticked on a lens set.

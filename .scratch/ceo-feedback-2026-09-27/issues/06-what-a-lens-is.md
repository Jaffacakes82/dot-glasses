# 06 — What a lens is: one lens database for lens sets and custom lenses

Type: grilling
Status: resolved
Blocked by: 05

## Question

The call agreed on one lens database, shaped like the custom lens (sphere, cylinder, axis, add,
lens type), that both custom lenses and lens-set lenses refer to, so a +3.00 is the same thing
wherever it was chosen. What exactly is a lens record, and what replaces the label-only
"Lens strengths" reference data?

Sub-questions: per eye or per pair; is a lens a stored row or a value; does a lens-set entry carry a
simplified display label over it; does "Custom" stay a separate lens range once custom lenses are
drawn from the same database; the product term (the CEO asked for "lens power" over
"lens strength").

## Context

- Feedback doc: "Lens range in pre-sets to link to the same database at the custom lenses";
  "Remove 'Lens strengths' list"; "Change language from 'lens strength', to 'lens power'".
- Call: the lens-set entry keeps a simple label ("+1.25") over the full lens behind it; a bifocal
  is distance 0 plus an add, not a special kind.
- Today: `LensOption` points at a `LensStrength` `ReferenceDataItem` (label only); custom lenses are
  typed `CustomSphere/Cylinder/Axis/AddPower` columns per eye with no link to it.
- Revisits part of ADR-0005 (lens sets are data-driven) — the data-driven part stands, the lens
  inside a set changes.

## Answer

Grilled 2026-09-28. Recorded in [ADR-0007](../../../docs/adr/0007-a-lens-power-is-a-value.md),
with a note added to ADR-0005. `CONTEXT.md` defines **Lens power** and **Lens type**, and its
**Lens range** and **Lens set** entries are updated to match.

**The model**
- **A lens power is a value, not a stored item.** It is one eye's sphere, cylinder, axis and add.
  The same numbers are the same lens, whether they came from a lens set or a custom prescription.
  (Q1)
- **The allowed values copy the online shop's configurator exactly.** They live in the Rules project
  and change only with a release (Q5, Q6):

  | | Allowed values | Step |
  |---|---|---|
  | Sphere | -10.00 to +10.00 | 0.25 |
  | Cylinder | 0.00 to -6.00 | 0.25 |
  | Axis | 0 to 180 | 1° |
  | Add | 0.00 to 3.00 | 0.25 |

  Pupil distance is 54–74 mm in 1 mm steps, one value per pair. It is not part of the lens power.
- **What makes a lens power valid** (Q7):
  - Sphere is required.
  - A blank cylinder means 0.00.
  - Axis is required only when cylinder isn't 0.
  - A blank add, or an add of 0.00, means no add. This fixes the Field App asking for lens type at
    +0.00.
- **Lens type is one per pair, never mixed.** Single vision is inferred when neither eye has an add;
  it is never asked and is not reference data. With an add, the choice is Bifocal, Progressive or
  Other (free text), which stay in reference data. (Q2, Q8)
- **Both lens ranges stay.** A lens set is ready-made stock. A custom prescription is any allowed
  power for each eye, made to order. Both produce the same kind of lens. (Q3)
- **"Lens power" replaces "lens strength"** on every screen, and the Lens strength reference data
  list is removed. (Q4)
- **A read-only "Lens powers" page** sits next to Lens Sets. It shows the allowed values and notes
  that changing them needs a release. (Q9)

**Lens sets**
- **An entry is:**
  - a lens power, entered with the same dropdowns as the shop;
  - a typed label, required and unique within its set, though other sets can reuse it;
  - a lens type, if it has an add;
  - the coatings it comes in, chosen when the lens is added. (Q10, Q14)
- **A set can hold the same power twice only with different lens types.** (Q11)

**Records**
- **Tests, Leads and Sales store the same shape:** each eye's lens power, one lens type and the lens
  range (which lens set, or custom). There is no pointer to the set entry, so editing a set never
  rewrites history. Coating *preference* on Tests and Leads is unchanged. (Q12, Q13, Q15)
- **Powers the app displays itself show `+` on positive values**, the clinical convention. The
  allowed values themselves are exactly the shop's. (Q16)

**Handed on**
- Ticket 07: choosing each lens's coatings when it's added to a set.
- Ticket 08: the shop-style dropdowns and the typed, required label that is unique within its set.
- Ticket 09: existing `LensOption` entries, `LensStrength` items, the coating grid, and nonprod
  records that point at them.
- Ticket 10: a lens set's two eyes must share a lens type.
- Ticket 12: the lens power LE/RE columns read the stored powers, with `+` on positive values.
- Ticket 15: the shop also asks for a dot colour, which the Field App doesn't capture.
- Not taken on: the shop's prescription upload and its "contact us for a quote" pricing. The Field
  App has no pricing.

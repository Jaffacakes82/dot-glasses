# 08 — The lens set admin screen for adding lenses

Type: prototype
Status: open
Blocked by: 06, 07

## Question

How does an admin add lenses to a lens set: the custom-lens-style input, a table for adding many,
CSV import in a defined format? Prototype the screen to react to.

## Context

- Feedback doc: "Use custom lens input to define lenses in preset catalogues ... A table format may
  be best to quickly add multiple. Or importing a CSV in a defined format."
- Call: few people will do this, rarely, so "if it's a bit cumbersome for the user I don't care".
- Feedback doc: assigning a lens set to an org gives no confirmation message.
- From ticket 06 (ADR-0007):
  - Adding a lens to a set should work like the online shop's configurator: sphere, cylinder, axis
    and add dropdowns with the shop's allowed values.
  - The admin also enters a typed label, which is required and unique within the set.
  - The lens type is asked for only when the lens has an add.
  - The coatings for the lens are chosen in the same step.
  - A read-only "Lens powers" page sits next to Lens Sets and lists the allowed values.
- From ticket 07 (ADR-0007, "Coatings"):
  - When adding a lens, the admin ticks the coatings it comes in, at least one.
  - The admin can add pairings for that lens only, such as "Blue block → Photochromic".
  - Saving a pairing that contradicts a global exclusion is refused.
  - The old "Lens strength coating availability" grid goes.

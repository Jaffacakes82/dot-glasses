# 08 — The lens set admin screen for adding lenses

Type: prototype
Status: resolved
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

## Answer

Prototyped and reviewed 2026-09-28. The prototype is on the throwaway branch
`prototype/lens-set-admin-screen` (commit `709752f`), at
`src/DotGlasses.Web/Views/Catalogues/prototype-lens-set-screen.html`. It is a single
self-contained file: double-click it to open. It holds three variants, switched with `?variant=`
or the floating bar.

**Verdict: variant A, the configurator dialog. There is no bulk add for now.**

- **Each lens set lists its lenses in a table.** The columns are label, lens power, lens type,
  coatings and pairings, with Edit and Remove on each row.
- **"Add lens" opens a dialog laid out like the online shop's configurator, one lens at a time.**
  It has these fields:
  - Spherical, Cylindrical, Axis and Add Near Vision Power dropdowns, with the shop's allowed values
    in the shop's order.
  - Axis is enabled only when cylinder isn't 0.
  - Lens type (Bifocal, Progressive or Other, with free text for Other) appears only when there's an
    add. Otherwise the dialog says the lens is single vision.
  - A typed label, required and unique within the set.
  - Coating checkboxes, with at least one required. A note lists the global exclusions that apply
    at sale time.
  - "Pairings for this lens": two dropdowns limited to the ticked coatings. A pairing that an
    exclusion forbids, or a duplicate pairing, is refused inline.
- **Edit reuses the same dialog.** Saving lists every problem at once, using the checks from
  tickets 06 and 07.
- **A read-only "Lens powers" tab** shows the allowed values and the validity rules.
- **Rejected for now:** the spreadsheet grid (variant B) and CSV import (variant C). Few people set
  up lens sets, and rarely, so bulk entry isn't needed yet. Both are recorded in the map's Out of
  scope.
- **Carried into the lens spec unchanged:** the feedback doc's request for a confirmation message
  after assigning a lens set to an org. This needs no design choice.

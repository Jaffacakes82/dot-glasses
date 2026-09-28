# 07 — Add, edit and remove a lens in the Add lens dialog

**What to build:** An admin builds a lens set one lens at a time in an "Add lens" dialog laid out like
the online shop: spherical, cylindrical, axis and add dropdowns (axis only when cylinder isn't 0), lens
type radios (Bifocal, Progressive, Other with text) only when there's an add, a typed label, coating
checkboxes (the global exclusions listed as a note), and "Pairings for this lens" with two dropdowns
limited to the ticked coatings. Saving lists every problem at once, keyed to the dialog's fields. The
same dialog edits a lens, and a lens can be removed. Each set's lens table shows label, lens power,
lens type, coatings, pairings, Edit and Remove, in the fixed Rules order.

**Blocked by:** 02, 04

**Status:** ready-for-agent

**Model:** Opus 5.5 — returning every validation problem into a modal on a POST-redirect-GET screen is
the subtle part, and the validator runs inside a reference-data write.

**Seam:** `DotGlasses.Web.Tests` — the Lens Sets screen over HTTP.

**Don't run alongside:** 03, 08 (Lens Sets controller and view).

## Acceptance criteria

- [ ] The dialog follows prototype variant A (branch `prototype/lens-set-admin-screen`, reference only,
      never merge): dropdown values and order come from the Rules allowed values.
- [ ] Server checks, all returned together: label required and unique within the set
      (case-insensitive); power valid (Rules helper); lens type required with an add; at least one
      coating; power plus lens type unique within the set (same power allowed with different lens types);
      each pairing uses ticked coatings, isn't self-paired or duplicated, and isn't forbidden by an
      exclusion.
- [ ] The existing add-lens-option validator is replaced; it uses `IReferenceDataLookupService`, not the
      memoised snapshot (CLAUDE.md), and reuses the Rules power-validity helper.
- [ ] Edit reuses the dialog; Remove removes the lens.
- [ ] The table uses the Rules display order and format.
- [ ] Web.Tests: add, edit, remove; each refusal above, returned together.

## Notes

- Spec: `../spec.md` — "Admin Portal" (Lens Sets screen, "Add lens" dialog), user stories 4–17.
  Map ticket 08 for the prototype decision.
- Prior art: `RetireLensSetScreenTests`, `CataloguesScreenWordingTests`, `LensSetNameTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

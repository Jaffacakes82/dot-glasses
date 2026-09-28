# 07 — Add, edit and remove a lens in the Add lens dialog

**What to build:** An admin builds a lens set one lens at a time in an "Add lens" dialog laid out like
the online shop: spherical, cylindrical, axis and add dropdowns (axis only when cylinder isn't 0), lens
type radios (Bifocal, Progressive, Other with text) only when there's an add, a typed label, coating
checkboxes (the global exclusions listed as a note), and "Pairings for this lens" with two dropdowns
limited to the ticked coatings. Saving lists every problem at once, keyed to the dialog's fields. The
same dialog edits a lens, and a lens can be removed. Each set's lens table shows label, lens power,
lens type, coatings, pairings, Edit and Remove, in the fixed Rules order.

**Blocked by:** 02, 04

**Status:** resolved

**Model:** Opus 5.5 — returning every validation problem into a modal on a POST-redirect-GET screen is
the subtle part, and the validator runs inside a reference-data write.

**Seam:** `DotGlasses.Web.Tests` — the Lens Sets screen over HTTP.

**Don't run alongside:** 03, 08 (Lens Sets controller and view).

## Acceptance criteria

- [x] The dialog follows prototype variant A (branch `prototype/lens-set-admin-screen`, reference only,
      never merge): dropdown values and order come from the Rules allowed values.
- [x] Server checks, all returned together: label required and unique within the set
      (case-insensitive); power valid (Rules helper); lens type required with an add; at least one
      coating; power plus lens type unique within the set (same power allowed with different lens types);
      each pairing uses ticked coatings, isn't self-paired or duplicated, and isn't forbidden by an
      exclusion.
- [x] The existing add-lens-option validator is replaced; it uses `IReferenceDataLookupService`, not the
      memoised snapshot (CLAUDE.md), and reuses the Rules power-validity helper.
- [x] Edit reuses the dialog; Remove removes the lens.
- [x] The table uses the Rules display order and format.
- [x] Web.Tests: add, edit, remove; each refusal above, returned together.

## Notes

- Spec: `../spec.md` — "Admin Portal" (Lens Sets screen, "Add lens" dialog), user stories 4–17.
  Map ticket 08 for the prototype decision.
- Prior art: `RetireLensSetScreenTests`, `CataloguesScreenWordingTests`, `LensSetNameTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**Resolved** on `feat/lens-power-07-add-lens-dialog-cont`. The earlier agent's WIP commit held the
service, validator, controller action, view models and tests. This finished the dialog, its script
and the table wiring.

**What was built**
- `Views/Catalogues/_LensDialog.cshtml` is one dialog, rendered once after the lens-set cards. Every
  set's "Add lens" button and every lens's "Edit" button share it.
- `wwwroot/js/lens-dialog.js` fills the dialog from the button that opened it: blank for Add, or the
  lens's option values from the Edit button's `data-lens` JSON. It also keeps the fields consistent:
  - axis is enabled only when cylinder isn't 0.00;
  - lens type radios show only with an add, and the "Other" text only with Other. Hidden controls
    are disabled, so they aren't posted;
  - pairing dropdowns offer only ticked coatings;
  - pairing rows are renumbered from 0 after each add or remove, because model binding stops at
    the first gap.
- Dropdown values and text come from `LensPowerValues` in the shop's order. Option values use
  `LensDialogValues`, which formats in the request culture that the model binder parses with.
- The table is in `LensSetLenses.InDisplayOrder`, applied where the snapshot is built (B03/B04). It
  has Edit and Remove. Remove now asks for confirmation, like Retire does.
- `SaveLensRequestValidator` runs every check from the acceptance criteria.
  - It reads rows through `IReferenceDataLookupService` and
    `IPresetCatalogueAdminService.ListLensesForCheckAsync`, never the memoised snapshot.
  - It hands `LensPowerRules.Check`, `LensPowerRules.LensType` and `LensSetLenses.Match` a small
    literal snapshot.
  - Failures are keyed on the request's property names, which are also the dialog's field names.
    A pairing's failure is keyed `Pairings[i]`.

**The error round trip**
- A save that passes is written, then redirected to Index with the info banner (POST-redirect-GET).
- A refused save is **not** redirected. `SaveLens` renders Index straight from the POST, as
  `CreateCatalogue`, `UpdateCatalogue` and `AssignCatalogues` on this screen already do. The
  difference is that it also passes the admin's own posted form as `LensDialogViewModel.Reopen`.
  The partial then renders:
  - the dialog open (`data-open-on-load="true"`, which the script shows on load);
  - the admin's own input, including every pairing row as posted;
  - each ModelState message in the `data-error-for` slot of its field;
  - any message whose key has no slot (a model-binding error, say) at the foot of the dialog, so
    none is dropped.
- The page's top validation summary is suppressed while the dialog carries the errors. Without
  `Reopen`, the dialog shows no ModelState, because the errors belong to another form.
- A redirect was rejected. It would have to carry the whole form, the pairing rows and every keyed
  message through TempData, just to rebuild the same page. Nothing has been written when the page
  renders, so the page render can safely read the memoised snapshot.
- A business-rule refusal still goes through `DomainRuleViolationFilter`'s PRG. There are two:
  - the lens set was retired since the page loaded (`SaveLensAsync`);
  - the lens being edited was removed since the page loaded. This is new: it is a
    `DomainRuleViolationException` in the controller, not a 403. Editing a lens through another
    lens set is still a 403.

**Deviations and small decisions**
- Option `selected` is written by a small raw-HTML helper rather than through the `option` tag
  helper, so it appears exactly when it applies.
- A bug in the WIP view is fixed. `data-lens` was written with `Json.Serialize(...)`, which is raw
  HTML inside a quoted attribute and broke it. It now uses `.ToString()`, so the JSON is
  attribute-encoded. `EditingALens…` now parses the attribute and asserts its values.
- A Razor gotcha hit here: a `data-*` attribute with a null value renders empty rather than being
  dropped. `data-other` is therefore always `"true"` or `"false"`. It may be worth a CLAUDE.md
  pitfall line (for B12 to decide).
- `SaveLensAsync` now stores the "Other" free text only when the chosen lens type really is the
  LensType "Other" item. This is a direct row read, since it is a write path.
- The Add near vision power dropdown has no blank "None" option. 0.00 is first and means no add, as
  in the shop. Sphere and Axis have a blank "Select".
- Axis option text is the plain number, which the test pins. The prototype's "°" was dropped.
- No new design tokens. The styling is Bootstrap plus existing `dg-*` classes and inline styles.
- No migration.

**Checked in a browser.** The rendered HTML was dumped from the test host and served statically.
Checked: Edit fills every field and pairing, Add resets, cylinder/add/Other toggling, pairing
filtering and renumbering, and the reopened refused dialog.

**Stale docs for B12**
- CLAUDE.md's FluentValidation bullet still names `AddLensOptionRequestValidator` and
  `SetCoatingAvailabilityRequestValidator` as the `IReferenceDataLookupService` users. The user is
  now `SaveLensRequestValidator`. The bullet also says "ten remaining validators", but `Web` now has
  nine.
- CLAUDE.md's `PresetCatalogue`/`LensOption` domain bullet still describes the `LensStrength` roster
  and `LensStrengthCoatingOption`.
- `docs/functional-capabilities.md` needs the Lens Sets screen's Add lens dialog, Edit and Remove.
- `SaveLensRequestValidator`'s doc comment says it replaces the "Lens strength flow's"
  `AddLensOptionRequestValidator`. That is historical and accurate, but B12's wording sweep may
  want it rephrased.

**Full suite** (`dotnet test DotGlasses.sln`): 608 passed, 0 failed. By project: Rules 317,
Application 96, Infrastructure 62, Web 133.

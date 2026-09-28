# Spec — Lens power and lens sets

Status: ready-for-agent
Follows: `.scratch/multi-org-access-and-retail-points/spec.md`. Implement that first. Both specs
change the Lens Sets controller, and this one builds on Spec A's owning-org choice.
Source: wayfinder map `.scratch/ceo-feedback-2026-09-27/`, tickets 05–10. Decisions: ADR-0007
(including its "Coatings" section), which refines ADR-0001 and ADR-0005. Vocabulary: `CONTEXT.md`
(**Lens power**, **Lens type**, **Lens range**, **Lens set**, **Coating set**, **Coating
pairing**, **Coating exclusion**). Prototype of the admin screen: branch
`prototype/lens-set-admin-screen` (variant A was chosen).

## Problem Statement

The system doesn't know what a lens is.

- **A lens-set lens is only a label.** It points at a "Lens strength" reference item whose only
  content is text such as `+0.00 / +2.50 (Bifocal)`.
- **A custom prescription is typed numbers with no link to lens sets.** It stores sphere,
  cylinder, axis and add per eye. So a +3.00 sold from a lens set and a +3.00 entered as a custom
  prescription are unrelated, and nothing can report on the lens powers actually sold.

Several concrete problems follow:

- **Coatings follow the wrong thing.**
  - Which coatings a lens-set lens comes in is a global grid keyed on the label, so a lens offers
    coatings it isn't made in.
  - The server checks a Sale's coatings against the left lens only.
  - Pairings are global, but on a custom order anything can be paired.
- **The custom prescription doesn't match the online shop.**
  - It offers positive cylinders and cylinders down to -10.00.
  - Axis is a free number box.
  - It asks for lens type when the add is +0.00.
- **The lens section of the forms is hard to use.**
  - The technician picks the left and right lens separately, even when they're the same.
  - Coating preference and lens type are dropdowns, while a Sale's coatings are checkboxes.
  - Children's frame comes after pupil distance.
  - "Order this lens" comes before the coatings.
- **The product says "lens strength" where the CEO asked for "lens power".**

## Solution

A **lens power** is a value: sphere, cylinder, axis and add for one eye. Its allowed values are
copied from the online shop and change only with a release.

- **A Test, Lead or Sale stores each eye's lens power and one lens type.** It stores them the same
  way whether the lens came from a lens set or a custom prescription.
- **A lens set is a list of lenses.** Each has a lens power, a typed label, a lens type when it has
  an add, the coatings it comes in, and its own coating pairings.
- **Admins add lenses one at a time in a dialog laid out like the shop**, and can view the allowed
  values on a read-only Lens powers page.
- **On a lens set, the Field App offers:**
  - one "Lens" choice, with "Same lens for both eyes" ticked by default;
  - the chosen lens's power shown underneath;
  - only the coatings both lenses come in;
  - locked paired coatings.
- **The custom prescription copies the shop.**
- **Existing lens-set data is reset rather than converted.** The product isn't live.

## User Stories

1. As a DGI admin, I want every lens described by sphere, cylinder, axis and add, so that a +3.00 from a lens set and a +3.00 custom lens are recognised as the same lens.
2. As a DGI admin, I want the allowed values to match the online shop exactly, so that the field and the shop offer the same lenses.
3. As an admin, I want a read-only Lens powers page next to Lens Sets, so that I can see which values a lens can have.
4. As an admin, I want to add a lens to a lens set with the same dropdowns as the online shop (spherical, cylindrical, axis, add near vision power), so that building a set feels familiar.
5. As an admin, I want axis available only when cylinder isn't 0, so that I'm not asked for an axis that means nothing.
6. As an admin, I want to be asked for a lens type (Bifocal, Progressive or Other) only when the lens has an add, so that single-vision lenses need no extra step.
7. As an admin, I want to type the label technicians will see, so that I can keep it simple ("+2.50", "Bifocal +2.00").
8. As an admin, I want a label refused if another lens in the same set already uses it, so that technicians never face two identical choices.
9. As an admin, I want a lens refused if the set already has the same power with the same lens type, so that sets have no duplicates.
10. As an admin, I want the same power allowed twice when the lens types differ (Bifocal and Progressive), so that I can offer both.
11. As an admin, I want to tick the coatings each lens comes in, with at least one required, so that technicians are only offered what DGI makes.
12. As an admin, I want to add pairings for a single lens (e.g. "Blue block → Photochromic"), so that I can record what DGI manufactures for that lens.
13. As an admin, I want a pairing limited to the coatings I've ticked for that lens, so that it can only refer to real options.
14. As an admin, I want a pairing refused if an exclusion forbids those two coatings together, so that I can't configure something the Field App would then reject.
15. As an admin, I want every problem listed at once when I save a lens, so that I can fix them in one go.
16. As an admin, I want to edit a lens in the same dialog, so that there's one way to change it.
17. As an admin, I want to remove a lens from a set, so that I can correct a set.
18. As an admin, I want a confirmation message after assigning a lens set to an org, so that I know it worked.
19. As a DGI admin, I want "Lens strengths" gone from Reference Data, so that there's no list whose edits do nothing.
20. As a DGI admin, I want the global Pairings section gone from Coatings & tints, while exclusions stay, so that pairings live only on lens-set lenses.
21. As a DGI admin, I want "lens power" used instead of "lens strength" on every screen, so that the product speaks the business's language.
22. As a technician, I want one "Lens" dropdown with "Same lens for both eyes" ticked by default, so that the usual case is one choice.
23. As a technician, I want to untick "Same lens for both eyes" and choose left and right separately, so that I can serve someone whose eyes differ.
24. As a technician, I want the right eye limited to lenses with the left eye's lens type, so that I can't sell a mixed pair.
25. As a technician, I want lenses listed single vision first by sphere, then bifocal, progressive and other by add, so that I can find a lens quickly.
26. As a technician, I want the chosen lens's power shown under the dropdown, so that I can check what's behind a vague label.
27. As a technician, I want only the coatings both chosen lenses come in, so that I never offer a coating that can't be made.
28. As a technician, I want a paired coating ticked and locked, with "Comes with Blue block on this lens", so that I understand why I can't untick it.
29. As a technician, I want a ticked coating removed with a note when a lens change makes it unavailable, so that I'm not surprised at save time.
30. As a technician recording a custom prescription, I want the shop's dropdowns for each eye (sphere, cylinder 0.00 to -6.00, axis 0–180, add 0.00 to +3.00), so that I enter prescriptions the same way customers do online.
31. As a technician, I want the axis dropdown only when that eye's cylinder isn't 0, so that I skip it for most lenses.
32. As a technician, I want lens type asked only when an eye has an add above 0, so that an add of 0.00 doesn't trigger it.
33. As a technician, I want coatings as checkboxes, and lens type and coating preference as radio buttons, never dropdowns, so that the choices look consistent and are all visible.
34. As a technician, I want the lens section to run lens range, lens, children's frame, pupil distance, coatings, then "Order this lens" (custom only), so that the form follows the order I work in.
35. As a technician recording a Test or Lead, I want the coating preference limited to what both lenses come in on a lens set, so that the later Sale can honour it.
36. As a technician converting a Lead, I want its lens pre-selected by matching power and lens type in the same set, so that I don't re-enter it.
37. As a technician converting a Lead whose lens has since been removed from the set, I want that eye left empty with a note naming the lens and the set, so that I know to choose again.
38. As a technician correcting a record from Failed records, I want its lens pre-selected the same way, so that correcting it behaves like converting.
39. As an admin converting a Lead on the Admin Portal, I want the same rules and field order, including "Same lens for both eyes", so that both apps capture lenses alike.
40. As DGI, I want the server to enforce every lens rule the Field App shows, so that no client can store a lens DGI can't make.
41. As DGI, I want existing lens sets retired and emptied rather than converted, and old records to keep their set's name, so that history stays readable while the real sets are rebuilt.
42. As a developer, I want example 6-Lens and 9-Lens sets in the new shape in local dev and tests, so that the app is usable without building sets by hand.

## Implementation Decisions

**Rules** (the module the device and the server share)
- **Allowed values.** A single definition of the allowed lens power values, owned by Rules:
  - sphere -10.00 to +10.00, in 0.25 steps, listed in the shop's order (0.00 first);
  - cylinder 0.00, then -6.00 to -0.25, in 0.25 steps;
  - axis 0 to 180, whole degrees;
  - add 0.00 to 3.00, in 0.25 steps;
  - pupil distance 54 to 74 mm, whole millimetres, unchanged.

  It also owns the display format (`+` on positive values, two decimals). The Field App, the
  Admin Portal dialog, the Lens powers page and every validator read it. Nothing else lists these
  values.
- **Lens power validity:**
  - sphere is required;
  - a blank cylinder means 0.00;
  - axis is required when cylinder isn't 0, and must be empty when it is;
  - a blank add, or an add of 0.00, is normalised to no add.
- **Lens type:**
  - one per pair;
  - required when either eye has an add above 0;
  - must be empty otherwise, which means single vision;
  - Other needs its free text.
- **The lens-set branch of the consultation rules.** Each eye's lens power, plus the lens type,
  must match an entry in the chosen lens set, which must reach the location (as today). Both eyes'
  entries must share the lens type.
- **The coating rules.**
  - **On a lens set:** coatings must be ones both entries come in. For every trigger coating
    present from either entry's pairings, the paired coating must be present too. Global
    exclusions apply.
  - **On a custom prescription:** any active coating, subject to exclusions, with no pairings.
  - **At least one coating** on a Sale, as today.
  - **Coating preference** on a Test or Lead, when both lenses come from a lens set, must be one
    both entries come in.
- **New pure helpers in Rules**, used by the Field App, the Admin Portal and the server:
  - the fixed display order of a set's entries: single vision by sphere, then Bifocal, Progressive
    and Other, each by add, then by sphere;
  - matching a lens power and lens type to an entry in a set, used for conversion seeding and
    Failed-record correction;
  - the "offered coatings" and "required pairings" for a chosen pair.
- **`SaleAssembly.Seed`.** It carries each eye's lens power and the lens type instead of entry
  ids. `SaleAssemblyTests`' reflection check keeps every request property accounted for.

**Contracts** (breaking changes, accepted because the product isn't live)
- **Create requests.** `CreateTestRequest`, `CreateLeadRequest` and `CreateSaleRequest`:
  - drop `LensOptionLeftId` and `LensOptionRightId`;
  - rename the per-eye `Custom*` properties to range-neutral names (sphere, cylinder, axis and add
    for left and right);
  - use them for both lens ranges.

  `PresetCatalogueId` and `LensTypeRefId` stay, and a null lens type means single vision. The rule
  failure keys follow the new property names. `FormErrors`, `ValidationProblemDetails` and the
  Admin Portal's `Form.{PropertyName}` remap in lead conversion must all move with them.
- **The lens-set DTO.** It carries, for each entry: id, label, sphere, cylinder, axis, add, lens
  type id, coating ids, and pairings as trigger/paired id pairs. Global pairings disappear from the
  reference-data payload; exclusions stay.

**Domain and schema**
- **A lens-set entry** (`LensOption`, or "lens set lens" in new code comments) holds:
  - label, sphere, cylinder, axis, add, lens type reference;
  - its coatings (a new child table);
  - its pairings (a new child table: entry, trigger coating, paired coating).

  It drops the Lens strength reference and its sort order.
- **Records.** Tests, Leads and Sales rename their `Custom*` lens columns to range-neutral names,
  and drop the two lens-option id columns.
- **Removed:** `LensStrengthCoatingOption`, global `CoatingPairing` and the `LensStrength`
  reference category. The enum's numeric value is retired, not reused. Coating exclusions are
  unchanged.
- **The migration.** It resets lens-set data in the same change as the schema:
  - it soft-deletes (retires) every existing lens set, setting `IsDeleted` and `DeletedAtUtc`;
  - it deletes their entries and assignments;
  - it deletes Lens strength items, the grid and global pairings;
  - it keeps exclusions.

  Old lens-set records keep `LensRangeType` and `PresetCatalogueId`, with empty lens powers. Custom
  records keep their values as recorded, even outside the new ranges.
- **Seed data changes need care in the migration.** Removing the `HasData` seeds for lens sets,
  entries, assignments, Lens strength items and the grid makes EF generate `DeleteData` for the
  seeded lens sets. Edit the generated migration so the two seeded lens sets are retired, not
  deleted.
- **Example sets for dev and tests.** The dev-only seeder, and the test fixtures, create example
  6-Lens and 9-Lens sets in the new shape: real powers, labels, coatings, one example pairing, and
  the same assignments as today. Staging and production get no active lens sets.

**Admin Portal**
- **Lens Sets screen.** Each set lists its lenses in a table: label, lens power, lens type,
  coatings, pairings, Edit and Remove. The order is the fixed Rules order.
- **"Add lens" opens a dialog.** It follows the prototype's variant A and is also used for Edit:
  - spherical, cylindrical, axis and add dropdowns from the allowed values, in the shop's order;
  - axis enabled only when cylinder isn't 0;
  - lens type radios (Bifocal, Progressive, Other with text), only with an add;
  - the typed label;
  - coating checkboxes, with the global exclusions listed as a note;
  - "Pairings for this lens", with two dropdowns limited to the ticked coatings.

  The server validates on save and returns every problem at once, keyed to the dialog's fields. The
  checks are:
  - the label is required and unique within the set (case-insensitive);
  - the power is valid;
  - the lens type is required with an add;
  - at least one coating is ticked;
  - the power and lens type are unique within the set;
  - each pairing uses ticked coatings, isn't self-paired or duplicated, and isn't forbidden by an
    exclusion.

  The existing validator for adding a lens option is replaced. It must use
  `IReferenceDataLookupService`, not the memoised snapshot, because it runs inside a write. It
  reuses the Rules helpers for power validity.
- **The coating availability grid** and its save action are removed.
- **The Lens powers page.** A read-only tab or section of the Lens Sets screen, behind the same
  policy. It shows each allowed value list and the validity rules, rendered from the Rules
  definition.
- **Assigning a lens set** shows a success message after the redirect.
- **Reference Data.** The Lens strengths category and the Coatings & tints Pairings section go.
  Exclusions stay.
- **Lead conversion** gets the same lens section as the Field App (see below), including "Same lens
  for both eyes" and the matching-based seeding.
- **Wording.** Every "lens strength" becomes "lens power", in copy, validation messages and page
  text.

**Field App**
- **The lens-range selector.** It follows ticket 10's answer:
  - **Lens set:** "Same lens for both eyes" is ticked by default, with one "Lens" dropdown.
    Unticking it shows left and right dropdowns, and the right eye is filtered to the left eye's
    lens type. Entries are listed by label in the Rules order, and each chosen lens's power is
    shown underneath.
  - **Custom:** each eye has the shop's dropdowns, and the axis dropdown appears only when that
    eye's cylinder isn't 0. Lens type radios appear only when an add is above 0.
  - **Order:** lens range, lens, children's frame, pupil distance, coatings or coating preference,
    then "Order this lens from Dot Glasses" (custom only, last).
- **The coating multi-selector.** It takes the offered coatings and required pairings from the
  Rules helpers.
  - A paired coating is ticked and locked, with the note "Comes with <trigger> on this lens".
  - A lens change that removes an offered coating unticks it, with a note.
- **Tick-style choices, not dropdowns.** Lens type and coating preference render as radio buttons.
  Coating preference offers "No preference" first.
- **Seeding.** Seeding from a Lead or Test, and from a Failed record, uses the Rules matching
  helper. An unmatched eye is left empty with the note "The <power> on this <Lead> is no longer in
  <set>. Choose a lens." "Same lens for both eyes" starts ticked only if the eyes match.
- **The cached reference data** takes the new lens-set shape. The IndexedDB cache refreshes on the
  next online load. An old-shape record still in the outbox is rejected at sync and lands on Failed
  records. That is accepted.

**Docs**
- **CLAUDE.md is updated in the same change:**
  - the domain bullet for `PresetCatalogue`/`LensOption`;
  - the description of the Rules module, including coatings and allowed values;
  - the FluentValidation bullet, since the validator changes;
  - the design-token note, if the lens powers page adds styling.
- **`docs/functional-capabilities.md`** is updated with the new Lens Sets screen and the Lens
  powers page.
- **The handover note stands:** production will have no active lens sets until DGI builds them,
  and DGI should review the coating list against the shop's.

## Testing Decisions

- **What a good test is.** It checks behaviour through public interfaces: the Rules functions'
  inputs and outputs, and HTTP responses and rendered screens. It uses real Postgres where data is
  involved, and asserts on what a user or client observes. It never touches internals such as query
  shape or private helpers, and no mocking library is used.
- **`DotGlasses.Rules.Tests` is the main seam**, since most of the logic is shared there. Prior art:
  `ConsultationRulesTests`, `SaleAssemblyTests`, and the existing reference-data snapshot tests.
  Cases:
  - Allowed values:
    - positive cylinder, cylinder below -6.00, sphere outside ±10 and an off-step value are all
      rejected;
    - add 0.00 is normalised to no add.
  - Axis is required with a cylinder and rejected without one.
  - Lens type:
    - required with an add;
    - rejected without one;
    - Other needs text;
    - one type per pair.
  - Lens-set records:
    - each eye must match an entry in the set by power and lens type;
    - a mixed pair is refused.
  - Coatings on a lens set:
    - only those both entries come in are accepted;
    - a trigger without its partner is refused, from either entry;
    - exclusions are enforced.
  - Coatings on a custom prescription: any active coating, subject to exclusions, with no pairing
    enforcement.
  - Coating preference on a Test or Lead is limited on a lens set.
  - The fixed entry order.
  - The matching helper: an exact match, no match, and matching when two entries differ only by
    lens type.
  - `SaleAssembly.Seed` carries powers and the lens type, and the reflection test still passes.
- **`DotGlasses.Web.Tests`, over HTTP.** Prior art: the lens-set screen and availability tests
  from #28, and the access-control tests. Cases:
  - Lens Sets:
    - add a lens;
    - edit a lens;
    - remove a lens;
    - each refusal listed above, all returned together;
    - the success message after assigning.
  - The Lens powers page renders the allowed values.
  - Reference Data no longer shows Lens strengths or Pairings.
  - The lens-set API returns entries with powers, coatings and pairings.
  - The create endpoints store powers for both lens ranges, and reject:
    - an old-shape request;
    - a lens-set power with no matching entry;
    - a coating outside the intersection;
    - a missing paired coating.
  - Admin Portal lead conversion with "Same lens for both eyes", and with a no-longer-present lens.
- **`DotGlasses.Infrastructure.Tests`, for the reset migration.** Prior art:
  `LensRangeMigrationTests`. It checks that:
  - existing lens sets end retired and empty;
  - Lens strength items, the grid and global pairings are gone;
  - exclusions are intact;
  - an old lens-set record still resolves its set's name;
  - an old custom record keeps its out-of-range values.
- **Field App behaviour is checked by hand in the browser.** There is no test project. Check each
  of these:
  - "Same lens for both eyes" and the right-eye filter;
  - the power line under the dropdown;
  - the shop-style custom form, with the axis dropdown gated on cylinder;
  - lens type appears only when an add is above 0;
  - locked paired coatings and their note;
  - the note when a coating is removed;
  - radio buttons for lens type and coating preference;
  - the order of the lens section;
  - Lead conversion seeding, including an unmatched lens;
  - an old-shape outbox record landing on Failed records.

## Out of Scope

- **Adding many lenses at once, whether by editable grid or CSV import.** Both were prototyped and
  set aside.
- **Event History's "lens power LE/RE" columns and the training-org marker** (map ticket 12). This
  spec only keeps Event History working with the renamed columns.
- **Ordering a custom lens from a Lead** (map ticket 11).
- **A default or remembered lens set** (map ticket 19).
- **The general question order of the consultation forms** (map ticket 13). That covers
  referred-or-treated last and occupation earlier.
- **Changing the coating list itself.** Yellow tint, Polarized and Sunglasses, and making Clear
  exclude every other coating, are admin data for DGI to review.
- **The shop's dot colour, prescription upload and quote pricing.**
- **Editing the allowed values from the Admin Portal.** They change only with a release.
- **Per-eye coating sets.**

## Further Notes

- **ADR-0007 is the reasoning of record.** Read it, including the "Coatings" section, before
  changing the lens model. Don't reintroduce stored lens rows or global pairings without revisiting
  it.
- **Rule failure keys are load-bearing** (CLAUDE.md). Renaming the lens properties changes the keys
  the Field App and the Admin Portal render errors against. Update every consumer in the same
  change, and check a server rejection still lands on the right control.
- **Staging will lose its lens sets.** On deploy, every existing lens set, including anything the
  CEO built in testing, becomes retired and empty. Tell the CEO before this ships.
- **This spec builds on Spec A.** Spec A moves the Lens Sets controller onto the combined scope and
  adds the owning-org choice. This spec builds on that version.

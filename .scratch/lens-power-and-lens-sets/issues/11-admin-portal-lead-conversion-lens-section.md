# 11 — The Admin Portal's Lead conversion gets the same lens section

**What to build:** An admin converting a Lead on the Admin Portal gets the same lens rules and field
order as the Field App: "Same lens for both eyes", the right-eye lens type filter, the power line, the
shop-style custom dropdowns, radio buttons, offered coatings with locked pairings, and matching-based
seeding with the note when the Lead's lens is no longer in the set.

**Blocked by:** 05, 06

**Status:** resolved

**Model:** Sonnet 5 — Razor view and view model over the Rules helpers; the server rules already exist.

**Seam:** `DotGlasses.Web.Tests` — the lead conversion screen over HTTP.

**Don't run alongside:** 06, 10.

## Acceptance criteria

- [x] The conversion screen's lens section matches the Field App's order and choices.
- [x] Seeding uses the Rules matching helper; an unmatched eye is left empty with the note.
- [x] A server rejection still lands on the right control (`Form.{PropertyName}` remap).
- [x] Web.Tests: conversion with "Same lens for both eyes"; conversion where the Lead's lens is no
      longer in the set shows the note and leaves that eye empty.

## Notes

- Spec: `../spec.md` — "Lead conversion", user story 39. CLAUDE.md: `SaleAssembly` (seed, not
  override) and the deliberately-not-unified "Custom range only" gate.
- Prior art: `LeadConversionLensSetTests`, `ConversionAtomicityTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**2026-09-28 — resolved.**

**What changed** (all in `DotGlasses.Web` plus tests)
- `Views/LeadConversion/Convert.cshtml` rewritten into the Field App's order: lens range, lens (or
  Custom prescription), children's frame, pupil distance, coatings, then "Order this lens from Dot
  Glasses"; the Sale details block keeps consent, frame colour and hard case.
  - Lens set: "Same lens for both eyes" (ticked to start), one "Lens" dropdown or left and right, labels
    in `LensSetLenses.InDisplayOrder` (no more "<set> — <label>"), the right eye limited to the left's
    lens type, and the chosen lens's power line under each dropdown. The pupil distance is a 0-4
    dropdown (0-2 with a children's frame).
  - Custom: the shop's dropdowns (custom PD is now a dropdown from `LensPowerValues.PupilDistanceMm`), axis only with a cylinder, lens type as radios only with an
    add above 0.00, "Other" text only when Other is ticked.
  - Coatings: rendered from the chosen pair's `LensSetLenses.CoatingsFor` (the Lead's own lenses when
    the lens carries over). Coatings not offered are hidden and disabled; a pairing's coating is
    ticked and locked with "Comes with <trigger> on this lens". No pair yet (Custom, or a lens still
    to pick) lists every coating and the rules decide on submit.
  - "Order this lens from Dot Glasses (Custom range only)" stays unconditional (not unified).
- `LeadConversionFormModel`: form-only `SameLensForBothEyes` (default false, so an unticked box binds;
  the GET seeds it true, or from whether the Lead's two eyes are the same lens). `ApplyLensRange` makes
  the one lens both eyes when ticked and drops the other range's pupil distance (`PupilDistanceMm` on a
  lens set, `PresetPupilDistanceBucket` on Custom). `LeadConversionViewModel`: `LensSets` (replaces
  `AvailableCatalogues`), per-eye notes, `OfferedCoatingIds`, `CoatingPairings`, `CoatingsNote`,
  `TickedCoatings()`.
- Controller: matching-based seeding kept; "no longer in the set" now gives the Field App's per-eye
  note "The <power> on this Lead is no longer in <set>. Choose a lens." (stands until that eye has a
  lens; one note when the eyes are the same lens). New `GET Leads/Convert/{id}/coatings?left=&right=`
  returns `{ offered, pairings }` from `CoatingsFor` (hierarchy-scoped through the Lead) so the script
  never restates the rule.
- `wwwroot/js/lead-conversion.js` (progressive enhancement): show/hide and disable the inactive blocks,
  refill the lens dropdowns on a range change, filter the right eye, power lines, axis/lens type
  gating, children's-frame pupil distance, and the coating fetch, untick-with-note and pairing locks.
  Without it a `<noscript>` style reveals every block and the server validates and re-renders as before.
- Rule failures keep the `Form.{PropertyName}` remap and now show under their own control
  (lens dropdowns on `SphereLeft`/`SphereRight`/`LensTypeRefId`, custom fields, PD, coatings) as well
  as in the summary.

**Deviations / judgement calls**
- The Lead-details card no longer says "lens is no longer in the lens set"; the per-eye note replaces it
  (the existing test's assertion was updated).
- With the script off, changing the range or unticking "Same lens" needs a submit to take effect (the
  server then re-renders the right blocks).
- Coatings for an un-chosen pair list all coatings (the Field App lists none until both eyes are
  chosen); the pair choice narrows and unticks them.
- Checked in a browser against the rendered HTML served statically with a mock coatings endpoint (the
  script's behaviour: range change, same-lens toggle, right-eye filter, power line, pairing lock,
  untick note, custom gating); not against the real app.
- `LensOptionCard.FormatLensPower` (Web) is reused for the power line/notes.

**Stale docs for B12**
- `docs/functional-capabilities.md`'s Admin Portal lead conversion section: lens dropdowns now list the
  chosen set's lenses, "Same lens for both eyes", radios, custom PD dropdown, offered coatings and
  locked pairings, the note wording, and the new `/coatings` helper endpoint.
- CLAUDE.md: the `SaleAssembly` bullet's "Admin Portal renders it unconditionally" is still true; the
  Convert view now has a script (`lead-conversion.js`) — no rule change.

**Tests.** New `LeadConversionLensSectionTests` (17): same lens for both eyes (default ticked, ignores a
posted right lens, unticked records each eye), mixed lens type refused on `SphereRight`, right-eye choices
limited to the left's type, lens order and power data, power line, unmatched eye left empty with the
note (unticked; and same-lens ticked with one note), note standing after a refusal then converting,
coating and pairing refusals on the coating control, custom rejection on its own control, offered
coatings and locked pairing for a carried-over lens, the `/coatings` endpoint, Custom lists every
coating, custom form gating. Full `dotnet test DotGlasses.sln` — 665 passed, 0 failed (Application 96,
Rules 345, Infrastructure 62, Web 162); baseline 648.

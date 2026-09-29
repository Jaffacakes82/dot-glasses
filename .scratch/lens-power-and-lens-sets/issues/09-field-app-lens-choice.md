# 09 — The Field App's lens choice

**What to build:** On a lens set the technician makes one "Lens" choice, with "Same lens for both eyes"
ticked by default; unticking it shows left and right choices, with the right eye limited to lenses of
the left eye's lens type. Lenses are listed by label in the fixed Rules order, and each chosen lens's
power is shown underneath. A custom prescription uses the shop's dropdowns for each eye, with the axis
dropdown only when that eye's cylinder isn't 0 and lens type radios only when an add is above 0. Lens
type and coating preference are radio buttons (coating preference offers "No preference" first). The
lens section runs: lens range, lens, children's frame, pupil distance, coatings or coating preference,
then "Order this lens from Dot Glasses" (custom only, last). When a converted Lead's or Failed record's
lens is no longer in the set, that eye is left empty with the note "The <power> on this <Lead> is no
longer in <set>. Choose a lens."; "Same lens for both eyes" starts ticked only if the eyes match.

**Blocked by:** 05

**Status:** resolved

**Model:** Sonnet 5 — Field App markup over the Rules helpers that already exist.

**Seam:** manual browser checklist (no Field App test project). Keep `dotnet build` green.

**Don't run alongside:** 05, 10 (`LensRangeSelector`, `CoatingMultiSelector`, `ConsultationForm`).

## Acceptance criteria

- [x] "Same lens for both eyes" (default ticked), the right-eye lens type filter, and the power line
      under each choice (Rules display format).
- [x] Lens-set lenses in the Rules order; custom dropdowns from the Rules allowed values; axis gated on
      cylinder; lens type radios gated on an add above 0.
- [x] Radio buttons, never dropdowns, for lens type and coating preference.
- [x] Section order as above.
- [x] Seeding (conversion and Failed-record correction) via the Rules matching helper, with the
      unmatched-lens note.
- [x] The cached reference data takes the new lens-set shape (the IndexedDB cache refreshes on the next
      online load).
- [ ] Manual checklist, recorded in Comments when done: same-lens and right-eye filter; power line;
      custom form with axis gating; lens type only with an add; radios; section order; conversion
      seeding incl. an unmatched lens; an old-shape outbox record lands on Failed records.

## Notes

- Spec: `../spec.md` — "Field App" (lens-range selector, tick-style choices, seeding), user stories
  22–26, 30–34, 36–38. Map ticket 10 for the decisions. Outbox rule in CLAUDE.md.
- Skills: `/implement`, then `/code-review`.

## Comments

**2026-09-28 — resolved (build green; Field App behaviour unverified in a browser).**

- **What changed** (all under `src/DotGlasses.App`):
  - `LensRangeSelector.razor` rewritten. Lens set: "Same lens for both eyes" (ticked by default) with
    one "Lens" dropdown, or unticked, left and right dropdowns with the right limited to the left's
    lens type. Lenses are listed by label in `LensSetLenses.InDisplayOrder`, with the chosen
    lens's power under the dropdown (`LensPowerText`, the Admin Portal's "SPH … · CYL … × axis · ADD …"
    wording). Custom: six `LensPowerSelect` dropdowns per pair over `LensPowerValues`; **axis is now a
    dropdown** (`LensPowerValues.Axis`, it was a number box), shown only while that eye has a
    cylinder; lens type is radios shown only with an add above 0. Section order in the selector:
    lens range, lens (or custom powers + lens type), children's frame, pupil distance.
  - `LensRangeSelection`: `SameLensForBothEyes`, per-eye missing-lens notes, `ChooseSameLens`,
    `SetSameLensForBothEyes`, `ChooseLeftLens` (drops a right lens of another type), `ChooseRightLens`,
    `NoteMissingLenses`, and `EyesMatch` (via `LensSetLenses.Match`, so "the same lens" has one
    definition). Everything still records powers through `RecordedAs`.
  - New `ReferenceDataRadios.razor` (lens type; coating preference with "No preference" first),
    `LensPowerSelect.razor`, `ReferenceData/LensPowerText.cs`, and `.dg-choice` / `.dg-lens-*`
    classes in the App's `dot-glasses.css` (plain classes, no tokens, nothing mirrored to Web).
  - `ConsultationForm.razor`: coating preference for Test/Lead is radios (`RenderCoatingPreference`);
    on a Custom Sale "Order this lens from Dot Glasses" moved after the coatings (last in the lens
    section); `ApplyLensRange` takes a source noun ("Lead" for a conversion, "record" for a Failed
    record), sets the unmatched-lens note `The <power> on this <Lead> is no longer in <set>. Choose a
    lens.` and starts "Same lens for both eyes" ticked only if the two recorded eyes are the same
    lens (both eyes unmatched but identical: ticked, one note).
  - `ReferenceDataClient`: the IndexedDB payload carries a `LensSetShape` (1). A cache written before
    it (no field) has its lens sets dropped on an offline load — old lenses would read as sphere 0.00 /
    no coatings and choosing one would record a prescription nobody made; the rest of the cache
    (items, exclusions) is kept, and the next online load replaces the payload. B03's null guards
    remain.
- **Deviations / judgement calls.**
  1. `AvailableCoatingIdsForSelectedPreset` (which feeds both the coating preference and, unchanged,
     `CoatingMultiSelector`) now returns `LensSetLenses.CoatingsFor(left, right).Offered` and is empty
     until both eyes have a lens — it was the left lens's coatings only. Needed because the lens
     selector can now leave the right eye unchosen and B06 widens the server rule to both lenses.
     `CoatingMultiSelector` itself is untouched; ticket 10 still owns locked pairings and the removal note.
  2. Every lens change (either eye, the same-lens toggle) fires `SelectionChanged`, which clears the
     coating selection/preference as before (previously only the left lens did).
  3. Custom dropdowns now use a canonical invariant option value ("2.5") on both sides. Before, a
     model value that arrived as `2.5` (JSON, a lens) never selected the option `2.50` — a converted
     or Failed Custom record would have shown blank dropdowns.
  4. `LensPowerText.Format` duplicates `LensOptionCard.FormatLensPower` in Web because I may not edit
     Rules; worth moving into Rules later.
  5. The unmatched note reads "on this record" (not "Lead") when correcting a Failed record.
- **Manual checklist — all UNVERIFIED, to be confirmed by a human in the browser.** (`Selector` =
  `LensRangeSelector.razor`, `Form` = `ConsultationForm.razor`.)
  - [ ] Same lens / right-eye filter: on a lens set, "Same lens for both eyes" is ticked and shows one
        "Lens" dropdown; choosing sets both eyes (Selector `OnSameLensChosen` → `ChooseSameLens`).
        Unticking shows left/right; with a Bifocal left chosen the right lists only Bifocals, and
        changing the left to another type empties a now-mixed right (`RightLensChoices`,
        `LensRangeSelection.ChooseLeftLens`). Re-ticking copies the left lens to the right.
  - [ ] Power line: the chosen lens's "SPH +2.50 · CYL -0.75 × 90 · ADD +1.00" (only the parts it has)
        appears under each lens dropdown (Selector `RenderPowerLine`, `LensPowerText.Format`); lenses
        are in single vision → Bifocal → Progressive → Other order (`OrderedLenses`).
  - [ ] Custom form: sphere/cylinder/add/axis are dropdowns with the shop's values, 0.00 first for
        sphere and cylinder, no positive cylinder (`LensPowerSelect` over `LensPowerValues`); the axis
        dropdown (0–180) appears only once that eye's cylinder isn't 0.00 and disappears, cleared,
        when it goes back to 0.00 (Selector `OnCylinderChanged`).
  - [ ] Lens type only with an add: radios Bifocal/Progressive/Other appear only when an add is above
        0.00 (an add of 0.00 doesn't); "Other" reveals a text box; removing the add clears the choice
        (Selector `OnAddPowerChanged`, `ReferenceDataRadios`).
  - [ ] Radios, never dropdowns: lens type, and coating preference on a Test and a Lead, with "No
        preference" first; choosing a lens after picking a preference clears it and the radio unticks
        visibly (Form `RenderCoatingPreference`, `ReferenceDataRadios` re-keying).
  - [ ] Section order: lens range, lens, children's frame, pupil distance, coatings (Sale) or coating
        preference (Test/Lead), then — Custom Sale only — "Order this lens from Dot Glasses" last, above
        frame colour (Selector markup order; Form Sale block).
  - [ ] Conversion seeding: converting a Lead on a lens set pre-selects its lens (both eyes matching →
        "Same lens" ticked; eyes differing → unticked with each side selected). Remove that lens from
        the set in the Admin Portal, reload the device data, convert again: the Lens dropdown is empty
        with "The SPH … on this Lead is no longer in <set>. Choose a lens."; saving without choosing is
        refused against the lens (Form `ApplyLensRange`, `LensSetLenses.Match`, `EyesMatch`).
        Correcting a Failed record does the same with "on this record".
  - [ ] Old-shape outbox record: a queued record carrying `lensOptionLeftId`/`lensOptionRightId` is
        rejected at sync (400 on `SphereLeft`/`SphereRight`) and lands on Failed records; opening it
        shows the error against the lens dropdown (Selector `FieldError`s; keys unchanged).
  - [ ] Old cache: with an IndexedDB `reference-data-cache` written before this change, going offline
        shows no lens sets (rather than zero-power lenses); back online refreshes it
        (`ReferenceDataClient.TryLoadFromCacheAsync`).
- **Stale docs for B12** (not edited here): `docs/functional-capabilities.md`'s Field App lens
  section (single "Lens strength" dropdowns per eye, number-box axis, dropdowns for lens type and
  coating preference, coating preference limited by the left lens); CLAUDE.md's UI section if it
  mentions the lens selector; `CONTEXT.md` needs no change.
- **Tests.** No Field App test project. `dotnet build DotGlasses.sln`: 0 errors (1 pre-existing
  EF1002 warning in Infrastructure.Tests). Server code untouched, so no test run.

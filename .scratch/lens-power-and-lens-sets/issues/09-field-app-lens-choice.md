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

**Status:** ready-for-agent

**Model:** Sonnet 5 — Field App markup over the Rules helpers that already exist.

**Seam:** manual browser checklist (no Field App test project). Keep `dotnet build` green.

**Don't run alongside:** 05, 10 (`LensRangeSelector`, `CoatingMultiSelector`, `ConsultationForm`).

## Acceptance criteria

- [ ] "Same lens for both eyes" (default ticked), the right-eye lens type filter, and the power line
      under each choice (Rules display format).
- [ ] Lens-set lenses in the Rules order; custom dropdowns from the Rules allowed values; axis gated on
      cylinder; lens type radios gated on an add above 0.
- [ ] Radio buttons, never dropdowns, for lens type and coating preference.
- [ ] Section order as above.
- [ ] Seeding (conversion and Failed-record correction) via the Rules matching helper, with the
      unmatched-lens note.
- [ ] The cached reference data takes the new lens-set shape (the IndexedDB cache refreshes on the next
      online load).
- [ ] Manual checklist, recorded in Comments when done: same-lens and right-eye filter; power line;
      custom form with axis gating; lens type only with an add; radios; section order; conversion
      seeding incl. an unmatched lens; an old-shape outbox record lands on Failed records.

## Notes

- Spec: `../spec.md` — "Field App" (lens-range selector, tick-style choices, seeding), user stories
  22–26, 30–34, 36–38. Map ticket 10 for the decisions. Outbox rule in CLAUDE.md.
- Skills: `/implement`, then `/code-review`.

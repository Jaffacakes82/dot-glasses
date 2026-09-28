# 11 — The Admin Portal's Lead conversion gets the same lens section

**What to build:** An admin converting a Lead on the Admin Portal gets the same lens rules and field
order as the Field App: "Same lens for both eyes", the right-eye lens type filter, the power line, the
shop-style custom dropdowns, radio buttons, offered coatings with locked pairings, and matching-based
seeding with the note when the Lead's lens is no longer in the set.

**Blocked by:** 05, 06

**Status:** ready-for-agent

**Model:** Sonnet 5 — Razor view and view model over the Rules helpers; the server rules already exist.

**Seam:** `DotGlasses.Web.Tests` — the lead conversion screen over HTTP.

**Don't run alongside:** 06, 10.

## Acceptance criteria

- [ ] The conversion screen's lens section matches the Field App's order and choices.
- [ ] Seeding uses the Rules matching helper; an unmatched eye is left empty with the note.
- [ ] A server rejection still lands on the right control (`Form.{PropertyName}` remap).
- [ ] Web.Tests: conversion with "Same lens for both eyes"; conversion where the Lead's lens is no
      longer in the set shows the note and leaves that eye empty.

## Notes

- Spec: `../spec.md` — "Lead conversion", user story 39. CLAUDE.md: `SaleAssembly` (seed, not
  override) and the deliberately-not-unified "Custom range only" gate.
- Prior art: `LeadConversionLensSetTests`, `ConversionAtomicityTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

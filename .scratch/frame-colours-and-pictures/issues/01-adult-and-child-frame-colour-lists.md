# 01 — Adult and child frame colour lists

**What to build:** The Reference Data screen has two frame colour lists, adult and child, each with
its own pictures and its own "Other". The current colours are the adult list. The child list starts
with "Other" only.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

## Acceptance criteria

- [ ] `ReferenceDataCategory` gains a child frame colour value with a new number. The existing
      frame colour value keeps its number and is labelled "Frame colours (adult)". The reserved
      Lens strengths value is not reused.
- [ ] Any `Contracts` copy of the enum and its mapping are updated.
- [ ] The Reference Data screen lists "Frame colours (adult)" and "Frame colours (child)", both
      showing the picture field.
- [ ] A migration seeds one active "Other" item in the child list. The adult colours are not
      copied.
- [ ] The reference data API returns both categories to the Field App.
- [ ] Web.Tests cover: both lists appear; an item added to the child list appears only there; the
      one-active-"Other"-per-category rule holds for the new list.

## Notes

- Spec: `../spec.md` — user story 1; "Two categories".
- No form behaviour changes here; that is ticket 02.
- Skills: `/tdd`, then `/code-review`.

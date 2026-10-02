# 01 — Adult and child frame colour lists

**What to build:** The Reference Data screen has two frame colour lists, adult and child, each with
its own pictures and its own "Other". The current colours are the adult list. The child list starts
with "Other" only.

**Blocked by:** None

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

## Acceptance criteria

- [x] `ReferenceDataCategory` gains a child frame colour value with a new number. The existing
      frame colour value keeps its number and is labelled "Frame colours (adult)". The reserved
      Lens strengths value is not reused.
- [x] Any `Contracts` copy of the enum and its mapping are updated.
- [x] The Reference Data screen lists "Frame colours (adult)" and "Frame colours (child)", both
      showing the picture field.
- [x] A migration seeds one active "Other" item in the child list. The adult colours are not
      copied.
- [x] The reference data API returns both categories to the Field App.
- [x] Web.Tests cover: both lists appear; an item added to the child list appears only there; the
      one-active-"Other"-per-category rule holds for the new list.

## Notes

- Spec: `../spec.md` — user story 1; "Two categories".
- No form behaviour changes here; that is ticket 02.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-01 — built.** `ReferenceDataCategory.FrameColourChild = 8` in Domain and Contracts (6 stays
retired), the mapping, the Reference Data screen's two cards ("Frame colours (adult)", "Frame
colours (child)"), and migration `AddChildFrameColourList` seeding the child list's "Other". Tests:
`FrameColourTests`.

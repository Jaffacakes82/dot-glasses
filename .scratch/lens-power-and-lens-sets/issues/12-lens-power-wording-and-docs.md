# 12 — "Lens power" wording everywhere, and the docs

**What to build:** Every screen, validation message and page says "lens power", never "lens strength",
and the repo's docs describe the new lens model.

**Blocked by:** 06, 07, 08, 09, 10, 11

**Status:** ready-for-agent

**Model:** Sonnet 5 — wording and documentation.

**Seam:** `DotGlasses.Web.Tests` (a wording check in the style of `CataloguesScreenWordingTests`); the
full test suite passes.

**Don't run alongside:** anything still in flight in this spec.

## Acceptance criteria

- [ ] No user-visible "lens strength" remains in either app (copy, validation messages, page text).
- [ ] CLAUDE.md: the `PresetCatalogue`/`LensOption` domain bullet; the Rules module description
      (coatings, allowed values); the FluentValidation bullet (the replaced validator); the design-token
      note if new styling was added. Remove references to the grid and `LensStrengthCoatingOption`.
- [ ] `docs/functional-capabilities.md` describes the new Lens Sets screen and the Lens powers page;
      `docs/open-issues.md` drops the "strength with zero coatings" item and anything else fixed.
- [ ] The handover note stands (production has no active lens sets until DGI builds them; DGI to review
      the coating list against the shop's) — keep it in the map, don't delete it.

## Notes

- Spec: `../spec.md` — "Wording", "Docs", user story 21.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

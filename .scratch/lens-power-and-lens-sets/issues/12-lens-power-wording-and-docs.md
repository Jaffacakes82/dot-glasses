# 12 — "Lens power" wording everywhere, and the docs

**What to build:** Every screen, validation message and page says "lens power", never "lens strength",
and the repo's docs describe the new lens model.

**Blocked by:** 06, 07, 08, 09, 10, 11

**Status:** resolved

**Model:** Sonnet 5 — wording and documentation.

**Seam:** `DotGlasses.Web.Tests` (a wording check in the style of `CataloguesScreenWordingTests`); the
full test suite passes.

**Don't run alongside:** anything still in flight in this spec.

## Acceptance criteria

- [x] No user-visible "lens strength" remains in either app (copy, validation messages, page text).
- [x] CLAUDE.md: the `PresetCatalogue`/`LensOption` domain bullet; the Rules module description
      (coatings, allowed values); the FluentValidation bullet (the replaced validator); the design-token
      note if new styling was added. Remove references to the grid and `LensStrengthCoatingOption`.
- [x] `docs/functional-capabilities.md` describes the new Lens Sets screen and the Lens powers page;
      `docs/open-issues.md` drops the "strength with zero coatings" item and anything else fixed.
- [x] The handover note stands (production has no active lens sets until DGI builds them; DGI to review
      the coating list against the shop's) — keep it in the map, don't delete it.

## Notes

- Spec: `../spec.md` — "Wording", "Docs", user story 21.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

**2026-09-28 — resolved.** Every stale statement listed in tickets 01–11's comments was checked against
the code on this branch before it was rewritten.

**Wording**
- `grep -i strength` over `src/` finds no user-visible copy: nothing in a view, component, script or
  validation message says it. What remains is code comments that record the retirement
  (`ReferenceDataCategory` in Domain and Contracts, the seed configuration, `LensOption`,
  `LensOptionCoating`, `ReferenceDataController`). `SaveLensRequestValidator` and
  `IReferenceDataLookupService` had comments describing the "Lens strength flow"; both now describe what
  the code is.
- One client-visible message changed, deliberately: "…configured as available for the chosen lens option
  (see Lens Sets)." is now "…for the chosen lenses (see Lens Sets)." (the coating and the coating
  preference messages in `ConsultationRules`), since the rule is about two lenses. The assertions
  that pinned the old text (`ConsultationRulesTests`, `LensSetCoatingApiTests`) were updated in the same
  change. No other message was touched.
- New `Web.Tests/Catalogues/LensPowerWordingTests` (8): `/Catalogues`, `/Catalogues/LensPowers`,
  `/ReferenceData`, the Lead conversion screen, a refused Add lens dialog and both scripts contain no
  "strength"; the Lens Sets screen says "Lens powers". The Field App has no test project, so its copy
  is covered by the source sweep above.

**Docs**
- `CLAUDE.md`: new bullet after the `Rules` bullet for `LensPowers` (`LensPowerValues`, `LensPowerRules`)
  and `LensSets.LensSetLenses` (`InDisplayOrder`, `Match`, `CoatingsFor`, `RecordedAs`) and the lens-set
  branch of `ConsultationRules`; the FluentValidation bullet (nine validators, `SaveLensRequestValidator`
  as the `IReferenceDataLookupService` user); the scoping list; the `PresetCatalogue`/`LensOption` bullet
  (a lens is a lens power with label, lens type, own coatings and pairings; records hold no lens id);
  the `ReferenceDataItem` category list (Lens types; the Lens strengths category retired, value
  reserved); the RBAC table row (no grid); the offline-sync paragraph (`ReferenceDataClient.LensSetShape`);
  and one Common pitfalls line — a `data-*` attribute renders a `bool` as `"True"`/`"False"` and a `null`
  as an empty attribute, confirmed by rendering a probe attribute through the Admin Portal (removed
  again). The design-token note needed no change: the `.dg-choice`/`.dg-lens-*` classes ticket 09 added
  to the App's stylesheet are plain classes, not tokens.
- `docs/functional-capabilities.md`: §2.1 scoping list; §4.4 lead conversion (the lens section, the
  script, the `/coatings` endpoint); §4.6 Lens Sets (what a lens is, the lens table, the Add lens dialog
  with Edit and Remove, the assign confirmation, coatings and pairings living on the lens, the Lens powers
  page, what ships); §4.8 Reference Data (Lens types card, Exclusions, no Lens strengths or Pairings, the
  seed counts); §5.4/5.5 coating preference and coating; §5.6 rewritten for Same lens for both eyes, the
  right-eye filter, power line, coatings from both lenses, locked pairings, the removal note, seeding
  notes, the server's per-eye rules and the Custom range values (cylinder 0.00 then -6.00 to -0.25, axis
  dropdown only with a cylinder, lens type radios only with an add — the old "+0.25" cylinder claim was
  already wrong); §6 old-shape outbox and cache; §7 the `preset-catalogues` shape and the previously
  undocumented `coating-rules` endpoint.
- `docs/open-issues.md`: dropped the "12 of 16 strengths have no coatings" and "`LensStrength` is only a
  label list" items; added the go-live note (production has no active lens sets until DGI builds them;
  DGI to review the coating list against the shop's), the unverified-in-a-browser Field App lens and
  coating checklists (tickets 09 and 10), and the two-way pairing quirk. The handover note in
  `.scratch/ceo-feedback-2026-09-27/map.md` is untouched.
- `docs/adr/0002-…`: a short dated "Refined by ADR-0007" note under the title (the convention ADRs 0001
  and 0005 already use) instead of rewriting the pairings-in-snapshot line.

**Full suite** (`dotnet test DotGlasses.sln`): 685 passed, 0 failed (Rules 345, Application 99,
Infrastructure 69, Web 172); baseline 677 + 8.

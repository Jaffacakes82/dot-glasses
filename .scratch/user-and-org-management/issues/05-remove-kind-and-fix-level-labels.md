# 05 — Remove organisation Kind; levels read as words

**What to build:** The Kind box disappears from the Organisations screen and the database. Levels
read "DGI", "Country", "Retailer/distributor" and "Retail Point" wherever a level is shown.

**Blocked by:** None

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

## Acceptance criteria

- [x] `OrganisationNode.Kind` is removed: entity, configuration, a migration dropping the column,
      seed data, `CreateChildOrganisationRequest`, the Add dialog, the selected-organisation panel
      and the CSV export.
- [x] One helper produces the level label, used by the tree badge, the selected-organisation
      panel, the Add dialog and the CSV export.
- [x] No view compares against the display string to decide behaviour (the Add dialog currently
      tests `Type != "RetailPoint"`); it compares the level itself.
- [x] CLAUDE.md's `OrganisationNode` bullet no longer mentions a free-text `Kind` label.
- [x] Web.Tests cover: the page and the CSV carry no Kind; the labels read as specified.

## Notes

- Spec: `../spec.md` — user stories 20–21; "Organisations screen".
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-01 — built.** `OrganisationNode.Kind` is gone (migration
`RemoveOrganisationKindAddDeactivationGroup`). `OrganisationLevelLabels.For` is the one place a
level becomes words; `OrgNode` now carries the level itself and views test `IsRetailPoint`. The
"Add ..." button and dropdown use the same labels ("Add Retail Point"), replacing "Country
office"/"Retail point". Tests: `OrganisationsManagementTests`, `UserEditPlanTests`.

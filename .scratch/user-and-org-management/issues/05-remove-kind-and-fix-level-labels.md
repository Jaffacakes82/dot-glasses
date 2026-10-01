# 05 — Remove organisation Kind; levels read as words

**What to build:** The Kind box disappears from the Organisations screen and the database. Levels
read "DGI", "Country", "Retailer/distributor" and "Retail Point" wherever a level is shown.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

## Acceptance criteria

- [ ] `OrganisationNode.Kind` is removed: entity, configuration, a migration dropping the column,
      seed data, `CreateChildOrganisationRequest`, the Add dialog, the selected-organisation panel
      and the CSV export.
- [ ] One helper produces the level label, used by the tree badge, the selected-organisation
      panel, the Add dialog and the CSV export.
- [ ] No view compares against the display string to decide behaviour (the Add dialog currently
      tests `Type != "RetailPoint"`); it compares the level itself.
- [ ] CLAUDE.md's `OrganisationNode` bullet no longer mentions a free-text `Kind` label.
- [ ] Web.Tests cover: the page and the CSV carry no Kind; the labels read as specified.

## Notes

- Spec: `../spec.md` — user stories 20–21; "Organisations screen".
- Skills: `/tdd`, then `/code-review`.

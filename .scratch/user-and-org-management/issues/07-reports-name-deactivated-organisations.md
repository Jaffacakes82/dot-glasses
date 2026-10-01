# 07 — Reports keep a deactivated organisation's name

**What to build:** Records from a deactivated organisation keep counting everywhere, and the
dashboard, Event History and Custom Orders show the organisation's real name followed by
"(deactivated)" where they would otherwise show "Unknown outlet".

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP; `OrgTreeLookup` in `Application.Tests`.

## Acceptance criteria

- [ ] First confirm the current behaviour with a failing test: a record at a deactivated retail
      point shows as "Unknown outlet". If it already shows the name, record that in Comments and
      only add the marker.
- [ ] The unscoped organisation lookup returns deactivated organisations, flagged as such.
- [ ] `OrgTreeLookup` resolves outlet, Retailer and country through deactivated organisations and
      appends "(deactivated)" to a deactivated one's name.
- [ ] Counts are unchanged: the records were already included.
- [ ] The User Directory marks an assignment to a deactivated organisation "deactivated".
- [ ] Tests cover the dashboard's top lists and Event History's Outlet column for a deactivated
      retail point, and a retail point under a deactivated Retailer.

## Notes

- Spec: `../spec.md` — user stories 14 (the directory's marking; the picker's is ticket 03) and 26; "Organisations screen", last bullet.
- `UnscopedReportQueryService` re-applies soft-delete by hand today; that is the line to change.
- Skills: `/tdd`, then `/code-review`.

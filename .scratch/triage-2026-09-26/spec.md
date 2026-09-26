# Triage — Org hierarchy paths, Preset Catalogue fields — 2026-09-26

Source: three points raised by Joe on 2026-09-26: two questions about the data model and screen
copy, and one exception seen in real testing. One issue per point, per this repo's issue-tracker
convention (`docs/agents/issue-tracker.md`). Point 3 splits into two tickets because the code fix
and the data repair in the affected environment are separate jobs with separate owners.

## Issues

| # | Title | Status |
|---|---|---|
| 01 | Org path segments are re-minted after deactivation, which creates duplicate `HierarchyPath`s | `done` (PR #25) |
| 02 | Repair the duplicate `/1/2/` org paths in the affected environment | `done` (nonprod repaired by hand, verified 2026-09-26) |
| 03 | Why `AspNetUsers` carries `HierarchyPath` as well as `OrgNodeId` | `wontfix` (question answered; FK confirmed present in staging) |
| 04 | Preset Catalogue "Field App picker role" and "Diopter range" are confusing, and "Other" catalogues are dead config | resolved via grilling → 07–11, ADR-0005 |
| 05 | CI's migration step authenticates to Postgres by accident (blank username → `runner`) | `ready-for-human` |
| 06 | The error page's Request ID can't be found in the logs | `done` (PR #26) |
| 07 | Lens range is "a lens set" or "Custom prescription", driven by assigned lens sets | `done` (branch `feat/lens-sets`) |
| 08 | Retire and reactivate lens sets | `done` (branch `feat/lens-sets`) |
| 09 | Refuse a lens set that isn't available at the record's location | `done` (branch `feat/lens-sets`) |
| 10 | Lens set editing limited to the owning org; names unique among active sets | `ready-for-agent` |
| 11 | Catalogues screen speaks "lens set"; "Diopter range" dropped | `ready-for-agent` (after 07) |

## Ordering

- **02 blocks deploying 01 to the affected environment.** 01 adds a unique index on
  `OrganisationNodes.HierarchyPath`. CI's `dotnet ef database update` step fails in any environment
  that still holds duplicate paths. Until 02 is done there, the Dashboard, Event History and
  Custom Orders return 500 for **every** user in that environment, because all three build an
  `OrgTreeLookup` over the whole unscoped tree.
- 01 can be built and merged at any time. Environments without duplicates (e.g. a fresh local DB)
  are unaffected.
- 03, 04 and 06 are independent of everything else. 06 was raised later the same day.
- **Lens sets (07–11):** 07 goes first. It rewrites the lens-range rule branch (which 09 extends)
  and removes the picker-role field from the Catalogues screen (which 08, 10 and 11 also edit).
  After 07, the other four can be done in any order. 08/10 carry soft cross-references (the retire
  permission and "unique among *active*"), and each ticket says what to do if the other hasn't
  landed.

## Also noticed during triage

- The Deploy run for `9231e01` (2026-09-10) is still **waiting on the production approval gate**:
  staging deployed, production did not. The `OrgNodeId` foreign key discussed in 03 is therefore
  live in staging but not yet in production.

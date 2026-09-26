# Triage — Org hierarchy paths, Preset Catalogue fields — 2026-09-26

Source: three points raised by Joe on 2026-09-26: two questions about the data model and screen
copy, and one exception seen in real testing. One issue per point, per this repo's issue-tracker
convention (`docs/agents/issue-tracker.md`). Point 3 splits into two tickets because the code fix
and the data repair in the affected environment are separate jobs with separate owners.

## Issues

| # | Title | Status |
|---|---|---|
| 01 | Org path segments are re-minted after deactivation, which creates duplicate `HierarchyPath`s | `ready-for-agent` |
| 02 | Repair the duplicate `/1/2/` org paths in the affected environment | `needs-info` → then `ready-for-human` |
| 03 | Why `AspNetUsers` carries `HierarchyPath` as well as `OrgNodeId` | `wontfix` (question answered; FK confirmed present in staging) |
| 04 | Preset Catalogue "Field App picker role" and "Diopter range" are confusing, and "Other" catalogues are dead config | `needs-info` (product decision) |
| 05 | CI's migration step authenticates to Postgres by accident (blank username → `runner`) | `ready-for-human` |

## Ordering

- **02 blocks deploying 01 to the affected environment.** 01 adds a unique index on
  `OrganisationNodes.HierarchyPath`. CI's `dotnet ef database update` step fails in any environment
  that still holds duplicate paths. Until 02 is done there, the Dashboard, Event History and
  Custom Orders return 500 for **every** user in that environment, because all three build an
  `OrgTreeLookup` over the whole unscoped tree.
- 01 can be built and merged at any time. Environments without duplicates (e.g. a fresh local DB)
  are unaffected.
- 03 and 04 are independent of everything else.

## Also noticed during triage

- The Deploy run for `9231e01` (2026-09-10) is still **waiting on the production approval gate**:
  staging deployed, production did not. The `OrgNodeId` foreign key discussed in 03 is therefore
  live in staging but not yet in production.

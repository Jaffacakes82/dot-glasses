# Run plan — multi-org access (Spec A) and lens power (Spec B)

How to hand the tickets in `issues/` here and in `../lens-power-and-lens-sets/issues/` to sub-agents.
"A01" means this spec's ticket 01; "B01" means the lens spec's ticket 01.

## Branching

- **One integration branch per spec:** `feat/multi-org-access` (from `main`) and `feat/lens-power`
  (cut from `feat/multi-org-access` once A08 has merged into it). Every merge to `main` deploys to
  staging, where the CEO is testing, and a spec is only coherent once all its tickets land.
- **One branch and worktree per ticket**, cut from its spec's integration branch, named
  `feat/<spec>-NN-<slug>`. The orchestrating session merges each finished ticket into the integration
  branch (`--no-ff`) after checking it builds and its tests pass.
- **One PR per spec to `main`**, after `/code-review` on the whole integration branch against the spec.
  Spec A's PR goes first; `feat/lens-power` then merges `main` and opens its own.
- Ticket status: `ready-for-agent` → `claimed` when a sub-agent starts → `resolved` when merged into
  the integration branch, with a one-line note under `## Comments`.

## Progress

- 2026-09-28: waves 1–2 done. A01–A06 are merged into `feat/multi-org-access` and pushed, and the
  full suite passes (459 tests). Paused here to save usage. **Next:** wave 3 (A07), then A08 and
  A09.
- Follow-ups noted during review, not yet ticketed:
  - The Admin Portal's cookie recheck also runs on static-asset requests (one small query each).
  - Two admins removing a user's last two assignments at once could leave the user with none.
  - Several transitional "old active org counts as an assignment" rules are left for A08 to remove:
    the access loader, the User Directory listing and checks, the sidebar query, and invite
    filling the active-org columns.

## Models

Each ticket names its model and why. Opus 5.5 (`model: "opus"`) takes the current-user abstraction,
the filter, permission rules, migrations, the Rules rework and the contract renames. Sonnet 5
(`model: "sonnet"`) takes Razor/Blazor markup, wording and docs.

## Migrations run one at a time

A08 → B01 → B03 → B05, encoded as blocking edges. Never run two migration-bearing tickets at once:
they both rewrite `DotGlassesDbContextModelSnapshot`.

## Waves

| Wave | Run together | Notes |
|---|---|---|
| 1 | A01 | Foundation. |
| 2 | A02, A03, A04, A05, A06 | A03 and A06 share the user-assignment service — merge the second carefully. |
| 3 | A07 | |
| 4 | A08, A09 | A09 is Field App only; A08 changes fixtures. |
| 5 | A10, B01 | B01 needs A08 merged; `feat/lens-power` is cut here. |
| 6 | B02 | |
| 7 | B03 | |
| 8 | B04, B08 | Pure Rules helpers alongside a Lens Sets view. |
| 9 | B05, B07 | B05 is records/Rules/forms; B07 is the Lens Sets dialog. |
| 10 | B06, B09 | Server coating rules alongside Field App lens markup. |
| 11 | B10, B11 | Field App coating selector alongside Admin Portal conversion. |
| 12 | B12 | |

## Collision hotspots (never in flight together)

- EF migrations and the model snapshot.
- `CataloguesController` and the Lens Sets view: A04, B03, B07, B08.
- Rules' `ConsultationRules`, `ReferenceDataSnapshot`, `SaleAssembly`: B01, B02, B03, B05, B06.
- Field App `LensRangeSelector`, `CoatingMultiSelector`, `ConsultationForm`, and the Admin Portal's
  lead conversion view: A09, B01, B05, B09, B10, B11.
- The current-user abstraction and the hierarchy filter: A01, A06, A08.

## Before either spec ships (tell the user; don't act)

- Deploying Spec B retires every lens set in staging, including sets the CEO built. Tell the CEO first.
  Production will have no active lens sets until DGI builds them.
- Production deploys have waited for approval since 2026-09-10. Ticket 05 of
  `.scratch/triage-2026-09-26/` (the CI migration step's Postgres username) is still `ready-for-human`
  and must be fixed before a prod deploy is approved.

# 08 — Remove the active org

**What to build:** "Primary org" and the single active org disappear from the product entirely. The
upgrade keeps every existing user's former active org as one of their assignments, so no one loses
access when it ships. After this, nothing reads an org from claims: the Admin Portal uses the combined
scope and the Field App uses the current location.

**Blocked by:** 02, 03, 04, 06, 07

**Status:** ready-for-agent

**Model:** Opus 5.5 — a migration with a data step, and the contract step of the expand–contract
started in ticket 01.

**Seam:** `DotGlasses.Infrastructure.Tests` for the migration (a user whose former active org had no
assignment row has one after upgrade); `DotGlasses.Web.Tests` stays green throughout.

**Don't run alongside:** any other migration-bearing ticket (this is the first in the chain
08 → B01 → B03 → B05). Also avoid 05 and 09 if they're mid-flight — they shouldn't touch these
members, but the fixtures change here.

## Acceptance criteria

- [ ] The migration first inserts an assignment row for every user whose active org has none, then
      drops the user's active-org columns (org node id, path, level) and their foreign key.
- [ ] The single-org members of the current-user abstraction and their claims are removed; the
      transitional "old active org counts as an assignment" from ticket 01 goes.
- [ ] Anything still switching or reading the active org (including the old switch-active-org
      operation) is removed.
- [ ] The dev seeder and test fixtures seed assignments instead of active-org columns.
- [ ] Records already stamped above retail-point level are left untouched.
- [ ] Infrastructure.Tests cover the backfill: after upgrade, an existing user's former active org
      exists as an assignment.

## Notes

- Spec: `../spec.md` — "Schema and user management", "Other", user story 41.
- Prior art: `LensRangeMigrationTests`, `DevUserSeederTests`.
- Local gotchas (handoff): use Infrastructure as the EF startup project; set
  `ConnectionStrings__dotglassesdb` to a placeholder for design-time commands in a worktree; to undo
  an unapplied migration delete its files and `git checkout` the model snapshot. Migrations are
  applied only by CI.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

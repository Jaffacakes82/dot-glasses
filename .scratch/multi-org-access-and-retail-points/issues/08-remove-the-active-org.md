# 08 — Remove the active org

**What to build:** "Primary org" and the single active org disappear from the product entirely. The
upgrade keeps every existing user's former active org as one of their assignments, so no one loses
access when it ships. After this, nothing reads an org from claims: the Admin Portal uses the combined
scope and the Field App uses the current location.

**Blocked by:** 02, 03, 04, 06, 07

**Status:** resolved

**Model:** Opus 5.5 — a migration with a data step, and the contract step of the expand–contract
started in ticket 01.

**Seam:** `DotGlasses.Infrastructure.Tests` for the migration (a user whose former active org had no
assignment row has one after upgrade); `DotGlasses.Web.Tests` stays green throughout.

**Don't run alongside:** any other migration-bearing ticket (this is the first in the chain
08 → B01 → B03 → B05). Also avoid 05 and 09 if they're mid-flight — they shouldn't touch these
members, but the fixtures change here.

## Acceptance criteria

- [x] The migration first inserts an assignment row for every user whose active org has none, then
      drops the user's active-org columns (org node id, path, level) and their foreign key.
- [x] The single-org members of the current-user abstraction and their claims are removed; the
      transitional "old active org counts as an assignment" from ticket 01 goes.
- [x] Anything still switching or reading the active org (including the old switch-active-org
      operation) is removed.
- [x] The dev seeder and test fixtures seed assignments instead of active-org columns.
- [x] Records already stamped above retail-point level are left untouched.
- [x] Infrastructure.Tests cover the backfill: after upgrade, an existing user's former active org
      exists as an assignment.

## Notes

- Spec: `../spec.md` — "Schema and user management", "Other", user story 41.
- Prior art: `LensRangeMigrationTests`, `DevUserSeederTests`.
- Local gotchas (handoff): use Infrastructure as the EF startup project; set
  `ConnectionStrings__dotglassesdb` to a placeholder for design-time commands in a worktree; to undo
  an unapplied migration delete its files and `git checkout` the model snapshot. Migrations are
  applied only by CI.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

- Resolved on `feat/multi-org-access-08-remove-active-org`. Migration `20260928103802_RemoveUserActiveOrg`:
  `Up` first runs `INSERT INTO "UserOrgAssignments" ... SELECT ... WHERE "OrgNodeId" IS NOT NULL AND
  NOT EXISTS (...)` (deactivated orgs included — the assignment comes back into force on
  reactivation), then drops the FK, the index and the three columns. `Down` re-adds them in the
  "unassigned" shape (`HierarchyPath` `''`, the other two null) plus index and FK — lossy, commented
  as such; no access is lost since the pre-08 code also counted assignment rows. Tests/Leads/Sales
  are not touched. Pinned by `ActiveOrgMigrationTests` (Infrastructure.Tests): unbacked active org
  gets a row, already-backed one isn't duplicated, no active org gets nothing, a DGI-stamped record
  is unchanged, and the columns and every FK on `AspNetUsers` are gone.
- Removed: `ApplicationUser.OrgNodeId/HierarchyPath/OrgLevel`, `ApplicationUserConfiguration` (only
  held that FK), `ICurrentUserContext.OrgNodeId/HierarchyPathPrefix/OrgLevel`, the three
  `DotGlassesClaimTypes` org claims, `ApplicationUserClaimsPrincipalFactory` (Identity's default
  factory now serves both cookie and JWT — identity/role claims only), `AuthController`'s claim
  filter, and the transitional folds in `UserAccessLoader`, `UserAdminService` (listing,
  `AssignmentPaths`, invite, un-assign's active-org move) and `UserAssignmentsQueryService`.
  `IUserOrgAssignmentService`/`UserOrgAssignmentService` deleted outright — `ListAssignedOrgsAsync`
  and `SwitchActiveOrgAsync` were dead; `ListDgiOrCountryAssignmentsAsync` (+ `OwningOrgOption`)
  moved onto `IUserAssignmentsQueryService`, the other "the user's own assignments" read.
- **Found a missed single-org consumer:** `OrganisationAdminService.ListDeactivatedAsync` still read
  `HierarchyPathPrefix` from the cookie claim (so a multi-assignment admin saw deactivated orgs only
  under their old active org, and an empty claim meant *every* deactivated org). Now `LIKE ANY` over
  `ScopePaths`, failing closed. New test
  `OrganisationsScreenTreesTests.AnAdminAssignedToTwoCountries_SeesTheDeactivatedOrgsOfBoth_AndNoneOutsideThem`.
- Small additions: `InviteAsync` refuses an empty org list with `DomainRuleViolationException`
  (the removed active-org lookup used to throw on it incidentally; new
  `InviteAtomicityTests.InvitingAUserWithNoLocation_IsRefusedAndCreatesNothing`). Un-assign keeps
  its "User not found." `InvalidOperationException` via an `AnyAsync` check (pinned by
  `DomainRuleViolationScreenTests`). Nothing else in the invite transaction changed.
- Dev seeder: seeds only the account, role and `UserOrgAssignment` rows (idempotent, never removes).
  The dev Admin now has nested assignments (DGI + the Kenya retail point, so it can also record
  from the Field App). **Deviation:** no "assignments in separate trees" dev account — the seed data
  has one country, and adding one would mean new seeded orgs plus a new secret; the test fixture
  already has `TwoCountriesAdmin`. Fixtures (`AccessControlFixture`, `AdminPortalFactory`,
  `DevUserSeederTests`, Infrastructure test doubles) seed assignments only;
  `CreateAdminClient`'s unused `activeOrgNodeId` parameter was dropped. The switch-location test
  now asserts "writes nothing to the user row" via an unchanged `ConcurrencyStamp`.
- Coordination: nothing under `src/DotGlasses.App` or `src/DotGlasses.Contracts` touched; no
  endpoint removed (`my-orgs`/`switch-org` keep working). Tokens/cookies issued before deploy may
  still carry the old org claims — harmless, nothing reads them.
- **Stale docs for ticket 10:** CLAUDE.md "Data scoping" (filter "keyed off
  `ICurrentUserContext.HierarchyPathPrefix`"), the RBAC paragraph (`OrgLevelRequirement` "reads
  `ICurrentUserContext.OrgLevel`, denormalized onto `ApplicationUser.OrgLevel`, stamped as a
  JWT/cookie claim"); `docs/functional-capabilities.md` §§ around lines 281–294, 365, 394, 450
  (primary org, `OrgNodeId`/`HierarchyPath`/`OrgLevel` written at invite, stamping from the primary
  org).
- Full suite: `dotnet test DotGlasses.sln` — 472 passed, 0 failed (225 Rules.Tests + 96
  Application.Tests + 54 Infrastructure.Tests + 97 Web.Tests), up from 468 by the 4 new tests.

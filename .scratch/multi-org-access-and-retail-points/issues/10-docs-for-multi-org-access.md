# 10 — Update CLAUDE.md and functional capabilities for multi-org access

**What to build:** The repo's behavioural contract and capability doc describe the new access model,
so the next agent doesn't reintroduce the single active org.

**Blocked by:** 01, 02, 03, 04, 05, 06, 07, 08, 09

**Status:** resolved

**Model:** Sonnet 5 — documentation.

**Seam:** none (docs); the full test suite should still pass.

**Don't run alongside:** Spec B tickets that edit CLAUDE.md (B12) — merge conflicts only.

## Acceptance criteria

- [x] CLAUDE.md "Data scoping vs RBAC": scope is the union of assignments on the Admin Portal; the
      Field App is scoped to its current location; the filter matches any scope path.
- [x] CLAUDE.md "RBAC model": permissions use the highest assigned level and a check against any
      scope path; the new "all of the target's assignments in scope" rule for user actions.
- [x] CLAUDE.md's description of `ApplicationUser` and claims no longer mentions the active org or
      claim-based scope; access is re-read every request.
- [x] CLAUDE.md's offline-sync "known accepted risk" paragraph notes that records are now also refused
      when the location is no longer assigned.
- [x] `docs/functional-capabilities.md` describes the location picker, the "can't record here" screen,
      the sidebar footer and the owning-org choice. `docs/open-issues.md` drops anything this spec fixed.

## Notes

- Spec: `../spec.md` — "Other" (the CLAUDE.md bullet list). CLAUDE.md is a contract, not a changelog.
- Skills: `/implement`, then `/code-review`.

## Comments

- 2026-09-28 — Done on `feat/multi-org-access-10-docs`. Verified every claim against the code on
  this branch rather than ticket text alone (`ICurrentUserContext`/`UserAccess`/`CurrentLocation`
  in `Application/Common`, `OrgLevelRequirement`/`HierarchyDescendantRequirement`/
  `AllAssignmentsInScopeRequirement` in `Web/Authorization`, `NoLocation.razor`/
  `OutletSelect.razor`/`Home.razor`/`Settings.razor` in the Field App, `UserDirectory/Index.cshtml`,
  `Catalogues/Index.cshtml`).
  - **CLAUDE.md** ("Data scoping vs RBAC"): rewrote the data-scoping bullet for `ScopePaths`/
    `LIKE ANY`/union-of-assignments/fail-closed; added a bullet on the current-user abstraction
    (`UserAccess`, no active-org column, no scope/level/role claim, instant recheck, last-assignment
    refusal) and one on `CurrentLocation` being re-validated per request; rewrote the RBAC table's
    `Organisations.ManageInScope`/`Users.ManageInScope` rows and backing paragraph for
    `HighestLevel`/any-scope-path/all-assignments-in-scope; updated the offline-sync accepted-risk
    paragraph for the location-based refusal at sync. No section was restructured; only the stale
    sentences ticket 08 named, plus what the acceptance criteria asked for.
  - **`docs/functional-capabilities.md`**: fixed every stale spot ticket 08 named (primary org in
    §4.3 Organisations, §4.5 User Directory, §4.6 Lens Sets create) and added the location picker,
    the "can't record here" screen, the header/"Recording at" copy, the sidebar footer, the
    owning-org choice, and the User Directory rules (no primary org, last-assignment refusal, the
    server-only `ChangeRole` endpoint with no form yet, "Outside your scope"). Also corrected two
    passages the spec made actively wrong rather than leave them contradicting shipped behaviour:
    §2's "any role/level can record, stamped wherever" language (records are now refused off any
    retail point) and its DGI/Country persona row in the §3 matrix, and §7's `my-orgs`/`switch-org`/
    create-endpoint API rows (eligible-locations-only, three refusal messages, no more org-on-the-
    user-row). Left the pre-existing (pre-dating this spec) staleness in the doc's five-policy RBAC
    table alone — out of scope for this ticket, not something this spec touched.
  - **`docs/open-issues.md`**: nothing in the file referenced primary/active org, the access-refresh
    delay or anything else this spec fixed, so nothing was dropped. Added the three follow-ups named
    in the ticket: the Admin Portal's per-request recheck also querying on static-asset requests, the
    two-concurrent-admins last-assignment race, and A09's still-unverified manual Field App checklist.
  - `dotnet build DotGlasses.sln` — succeeds, 0 errors, 1 pre-existing unrelated `EF1002` warning
    (same one ticket 08 noted). Docs only; no test run.

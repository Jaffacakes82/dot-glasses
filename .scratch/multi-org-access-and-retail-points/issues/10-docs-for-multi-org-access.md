# 10 — Update CLAUDE.md and functional capabilities for multi-org access

**What to build:** The repo's behavioural contract and capability doc describe the new access model,
so the next agent doesn't reintroduce the single active org.

**Blocked by:** 01, 02, 03, 04, 05, 06, 07, 08, 09

**Status:** ready-for-agent

**Model:** Sonnet 5 — documentation.

**Seam:** none (docs); the full test suite should still pass.

**Don't run alongside:** Spec B tickets that edit CLAUDE.md (B12) — merge conflicts only.

## Acceptance criteria

- [ ] CLAUDE.md "Data scoping vs RBAC": scope is the union of assignments on the Admin Portal; the
      Field App is scoped to its current location; the filter matches any scope path.
- [ ] CLAUDE.md "RBAC model": permissions use the highest assigned level and a check against any
      scope path; the new "all of the target's assignments in scope" rule for user actions.
- [ ] CLAUDE.md's description of `ApplicationUser` and claims no longer mentions the active org or
      claim-based scope; access is re-read every request.
- [ ] CLAUDE.md's offline-sync "known accepted risk" paragraph notes that records are now also refused
      when the location is no longer assigned.
- [ ] `docs/functional-capabilities.md` describes the location picker, the "can't record here" screen,
      the sidebar footer and the owning-org choice. `docs/open-issues.md` drops anything this spec fixed.

## Notes

- Spec: `../spec.md` — "Other" (the CLAUDE.md bullet list). CLAUDE.md is a contract, not a changelog.
- Skills: `/implement`, then `/code-review`.

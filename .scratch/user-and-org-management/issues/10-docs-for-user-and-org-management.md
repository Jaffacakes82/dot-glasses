# 10 — Docs for user and organisation management

**What to build:** The repo's documents describe what tickets 01–09 built.

**Blocked by:** 01, 02, 03, 04, 05, 06, 07, 08, 09

**Status:** resolved

**Model:** Sonnet 5.5.

**Seam:** None — documents only.

## Acceptance criteria

- [x] `docs/functional-capabilities.md`: User Directory (Edit page, picker, column name),
      Organisations (bulk assign, no Kind, level labels, group deactivation, the strip), sign-in
      pages (forgot password), and the reports' naming of deactivated organisations.
- [x] `CLAUDE.md`: the RBAC section mentions the self-edit rules; the `OrganisationNode` bullet
      describes group deactivation and has no `Kind`.
- [x] `docs/open-issues.md`: the user-change history entry reflects that a log entry now exists;
      the manual checks for ticket 09 are listed until a person has done them.
- [x] The map's decision lines for tickets 03, 04, 17, 18 and 20 say "shipped" in place of "Not
      yet built".

## Notes

- Spec: `../spec.md`.

## Comments

**2026-10-01 — done.** `docs/functional-capabilities.md` §4.1, §4.3, §4.5, §5.1 and the API table;
`CLAUDE.md` (the `OrganisationNode` bullet and the RBAC section's self-edit rules);
`docs/open-issues.md`; the map's lines for tickets 03, 04, 17, 18 and 20.

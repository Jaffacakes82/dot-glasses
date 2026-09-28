# 02 — The Organisations screen shows one tree per part of the scope

**What to build:** An admin with assignments in separate parts of the hierarchy sees each part as
its own tree on the Organisations screen, highest level first (DGI, then Country, …) and alphabetical
within a level. Nested assignments (DGI plus a retail point under it) show only the outer tree, so no
org appears twice.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Sonnet 5 — a bounded change to one screen's tree-building, driven by the scope paths
ticket 01 provides.

**Seam:** `DotGlasses.Web.Tests` — the rendered Organisations screen.

**Don't run alongside:** 01.

## Acceptance criteria

- [ ] The Organisations screen builds one tree per scope path (from the current-user abstraction,
      not the old single-org prefix).
- [ ] Trees are ordered by level (DGI first), then by name.
- [ ] A user with nested assignments sees one tree, not two.
- [ ] Web.Tests cover a user with two countries (two trees, in order) and a DGI-plus-retail-point
      user (one DGI tree).

## Notes

- Spec: `../spec.md` — user stories 5–6, "Admin Portal screens". Ancestor names still resolve via
  `IUnscopedReportQueryService` (CLAUDE.md pitfall).
- Prior art: `AccessControlPolicyTests` (screen rendering after sign-in).
- Skills: `/implement` (with `/tdd`), then `/code-review`.

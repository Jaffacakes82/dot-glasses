# 01 — Admin Portal access is the union of a user's assignments, rechecked every request

**What to build:** A user's Admin Portal access comes from *all* of their org assignments combined,
read fresh on every request. A DGI admin who is also assigned to a retail point keeps DGI-wide data
and every DGI-level screen (Lens Sets, Custom Orders, Reference Data). Overlapping assignments count
each record once. Removing an assignment or changing a role takes effect on the user's next request,
and a suspended user is signed out of the Admin Portal (and gets 401 from the API) on their next
request. This is the fix for the CEO's "access drops to the lowest org" bug.

**Blocked by:** None — can start immediately.

**Status:** resolved

**Model:** Opus 5.5 — the current-user abstraction, the hierarchy filter (ADR-0004) and the
permission requirements are cross-cutting and security-critical.

**Seam:** `DotGlasses.Web.Tests` over HTTP (shared `WebApplicationFactory`, real Postgres). A pure
helper that collapses nested scope paths may also be unit-tested in `DotGlasses.Application.Tests`.

**Don't run alongside:** anything else — every other Spec A ticket builds on this one.

## Acceptance criteria

- [ ] The current-user abstraction gains, per request: the user's **scope paths** (assignment
      paths, with any path nested inside another removed), their **highest assigned level**, their
      role, and whether they are suspended. These are loaded from the database once per request and
      memoised for that request, never cached across requests. Claims supply only the user's identity.
- [ ] Transitional (expand step): until ticket 08 removes it, the user's old active org counts as one
      of their assignments when building the scope, so no one loses access before that migration's
      backfill. The existing single-org members stay in place for the consumers other tickets
      migrate (Organisations, User Directory, Lens Sets creation, the Field App API).
- [ ] The hierarchy filter shows a row when its `HierarchyPath` starts with *any* scope path, as one
      SQL predicate over an array parameter (e.g. `LIKE ANY`), on the raw string column (ADR-0004).
      No scope paths → no rows.
- [ ] Field App (JWT) requests keep today's scope (the token's org) for now — ticket 06 turns that
      into the validated current location. Cookie requests use the scope paths.
- [ ] The level requirement checks the highest assigned level; the descendant requirement passes
      when the target sits under any scope path. The sidebar's per-policy nav hiding follows.
- [ ] A cookie validation event runs on each request: a suspended or deleted user is signed out and
      redirected to sign-in. A JWT-validated event runs the same lookup and returns 401 when the
      user is suspended.
- [ ] Test fixtures seed at least one user with nested assignments (DGI plus a retail point under
      it) and one with assignments in separate trees (two countries).
- [ ] Web.Tests cover: a DGI-plus-retail-point user sees DGI-wide data; an overlapping record is
      counted once on the Dashboard; a Country-plus-retail-point user reaches Custom Orders and Lens
      Sets; removing an assignment directly in the database shrinks the scope on the next request;
      a role change is reflected on the next request; suspension redirects the next Admin Portal
      request to sign-in and returns 401 on the next API request.

## Notes

- Spec: `../spec.md` — "The current user", "Scoping and permissions", "Rechecking on every
  request". Decision: ADR-0006. Read ADR-0004 before touching the filter.
- Prior art: `AccessControlPolicyTests`, `HierarchyScopingFilterTests`, `DashboardTopPerformingTests`.
- Skills: `/implement` (with `/tdd` at the seam above), then `/code-review` against the spec.

## Comments

- 2026-09-28 — Done on `feat/multi-org-access-01-scope-union`. `UserAccess` (scope paths collapsed
  via `HierarchyPath.Outermost`, highest assigned level, role, suspension) is loaded by
  `IUserAccessLoader` from the cookie `OnValidatePrincipal` / JWT `OnTokenValidated` events and
  memoised in `HttpContext.Items` (`RequestUserAccess`), tagged with the user it was loaded for.
  The filter is `patterns.Any(p => EF.Functions.Like(HierarchyPath, p))` → `LIKE ANY`. Both
  requirements read role/level/scope from it. Old active org counts as an assignment
  (transitional); single-org members kept. JWT scope stays the token's org. A token or cookie for
  a user that no longer exists is refused too (401 / sign-out). Test harness now uses real
  accounts: `AdminPortalTestAuthenticationHandler` builds claims via the real factory and calls the
  loader; API tests use `CustomWebApplicationFactory.CreateTechnicianClient`. CLAUDE.md left to
  ticket 10.

# 06 — The Field App API works at one current location

**What to build:** The Field App works at one **current location**: an active retail point the user
is directly assigned to. "My orgs" returns only those eligible locations. Sign-in accepts the location
the device remembers and uses it if it's still eligible, otherwise the only eligible location if there
is exactly one, otherwise none. Switching location issues a new token and no longer writes to the user
row. Every Field App request is scoped to the current location only (Leads list, conversion Lead
match, reference data, lens-set availability), and the location is re-validated on every request.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Opus 5.5 — JWT events and the current-user abstraction.

**Seam:** `DotGlasses.Web.Tests` over HTTP (the auth API and the location-scoped reads).

**Don't run alongside:** 03 (shared user-assignment service).

## Acceptance criteria

- [ ] The JWT carries the current location id. On each JWT request the lookup validates it: still one
      of the user's *direct* assignments, Retail Point level, active. The current-user abstraction
      exposes the current location (id, path, name) and, when it isn't valid, why: no location, no
      longer assigned, deactivated, or not a retail point. An invalid location does not fail
      authentication.
- [ ] JWT requests are scoped to the valid current location only; with no valid location they see no
      scoped rows.
- [ ] "My orgs" returns only active, Retail Point level orgs the user is directly assigned to.
- [ ] Sign-in accepts an optional preferred location (an optional field on the login request, so the
      existing Field App keeps compiling) and issues a token carrying it if eligible, else the single
      eligible location, else none.
- [ ] Switch location issues a token with the chosen eligible location and writes nothing to the user
      row; an ineligible choice is refused.
- [ ] The Field App API controllers read the current location, not the old single-org prefix. (The
      create endpoints' refusals are ticket 07; leave them working for a valid location.)
- [ ] Web.Tests cover: "my orgs" returns only directly assigned active retail points; sign-in with a
      remembered location that is still eligible, no longer eligible, and where only one exists; the
      Leads list and lens-set availability are scoped to the current location.

## Notes

- Spec: `../spec.md` — "Auth API for the Field App", "The current user", user stories 22–25 and 33.
- Prior art: `LensSetAvailabilityApiTests`, `ChangePasswordApiTests`, `ConversionSourceScopingApiTests`.
- Skills: `/implement` (with `/tdd`), then `/code-review`.

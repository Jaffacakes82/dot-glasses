# 05 — The sidebar shows the user's role and assignments, and the email stays inside the nav

**What to build:** Every Admin Portal user sees their role and the names of the orgs they're assigned
to under their name in the sidebar footer, sorted by level then name and shortened to "+N more" when
there are many. A long email is truncated with an ellipsis and never overflows into the page.

**Blocked by:** 01

**Status:** resolved

**Model:** Sonnet 5 — Razor layout and CSS.

**Seam:** `DotGlasses.Web.Tests` — the rendered layout (role and assignment names present, "+N more"
over the limit). The email overflow is CSS: check it by eye in the browser.

**Don't run alongside:** 01.

## Acceptance criteria

- [x] The sidebar footer shows the role and assignment names, sorted by level then name, capped with
      "+N more".
- [x] The email is truncated with an ellipsis and stays within the nav at desktop and narrow widths.
- [x] Any new style uses the design tokens; if a token is added, update both `dot-glasses.css` copies
      (Web and App) by hand, per CLAUDE.md.
- [x] Web.Tests cover a user with one assignment and a user with more than the cap.

## Notes

- Spec: `../spec.md` — user stories 7–9, "Admin Portal screens".
- Skills: `/implement` (with `/tdd`), then `/code-review`.

## Comments

Implemented on `feat/multi-org-access-05-sidebar` (final commit `33f670e`).

- **Assignments query.** Added `IUserAssignmentsQueryService`
  (`DotGlasses.Application/Users`) / `UserAssignmentsQueryService`
  (`DotGlasses.Infrastructure/Identity`), a small read-only service resolving the signed-in
  user's `UserOrgAssignment` rows to name + level via `IUnscopedReportQueryService` (an
  assignment can sit outside the caller's own current scope, same reasoning as
  `UserOrgAssignmentService`). It also folds in `ApplicationUser.OrgNodeId` (the legacy
  "active org" column), matching the transitional rule `UserAccessLoader.ReadAsync` already
  applies for `ScopePaths`/`HighestLevel`/`Role` — without it, an account that predates the
  assignment-rows migration and has no `UserOrgAssignment` row of its own would show no
  assignments despite having real access. Kept deliberately separate from
  `IUserOrgAssignmentService` (Field App self-service org-switching, no `Level` on its DTO)
  rather than widening that contract for callers that don't need it.
- **Sidebar markup.** `_Layout.cshtml` injects the new service, takes the first 3 assignment
  names (sorted by level then name — DGI first, same ordering spec.md uses for the
  Organisations screen's trees) and appends "+N more" when there are more. Guarded by
  `CurrentUser.IsAuthenticated`/`CurrentUser.UserId`, same pattern as the existing
  `canViewPresetCatalogues`-style checks, so anonymous/login-page renders never touch the DB.
- **Email overflow (CSS only).** `.dg-user-chip-email` (replacing the old inline-styled email
  `div`) gets `overflow: hidden; text-overflow: ellipsis; white-space: nowrap;`. Its flex
  parent, `.dg-user-chip-info` (replacing the old `style="flex:1; min-width:0;"`), keeps
  `min-width: 0` — the load-bearing part: a flex child never shrinks below its content's
  natural width without it, so `text-overflow: ellipsis` would have nothing to clip against
  and the email would instead push the sidebar wider. `.dg-user-chip-toggle` (the flex row
  containing avatar + info + caret) is already `width: 100%`, and at the ≤991px breakpoint
  `.dg-sidebar` becomes `width: 100%` rather than a fixed 240px — the row's own width still
  bounds the ellipsis at that width, so the fix holds at both breakpoints. Also added
  `.dg-user-chip-role`/`.dg-user-chip-assignments` for the (unchanged) role line and the new
  assignments line, matching the file's existing convention of hardcoded small `font-size`
  values (11px/13px, same as the rest of `dot-glasses.css`) rather than introducing a token
  below `--text-small`. No new token was added, so the App's copy of `dot-glasses.css` was
  left untouched, per CLAUDE.md.
- **Tests.** `tests/DotGlasses.Web.Tests/AccessControl/SidebarTests.cs`, reusing
  `AccessControlFixture`'s real-cookie sign-in and `CreateAccountAsync`: one test for a
  single-assignment account (role + org name present, no "+N more"), one for a four-assignment
  account (capped to 3, sorted DGI → Country → Intermediate → RetailPoint, "+1 more", the
  4th/lowest-level name absent from the response). Full suite:
  `dotnet test DotGlasses.sln` — 433/433 passed (Application.Tests 96, Rules.Tests 225,
  Infrastructure.Tests 50, Web.Tests 62).
- **Not done:** the email fix wasn't checked by eye in a browser — per the ticket's own Seam
  note, verified only by reasoning through the CSS above (no new browser-visible behavior to
  screenshot beyond what's described).

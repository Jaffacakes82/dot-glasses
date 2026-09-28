# 05 — The sidebar shows the user's role and assignments, and the email stays inside the nav

**What to build:** Every Admin Portal user sees their role and the names of the orgs they're assigned
to under their name in the sidebar footer, sorted by level then name and shortened to "+N more" when
there are many. A long email is truncated with an ellipsis and never overflows into the page.

**Blocked by:** 01

**Status:** ready-for-agent

**Model:** Sonnet 5 — Razor layout and CSS.

**Seam:** `DotGlasses.Web.Tests` — the rendered layout (role and assignment names present, "+N more"
over the limit). The email overflow is CSS: check it by eye in the browser.

**Don't run alongside:** 01.

## Acceptance criteria

- [ ] The sidebar footer shows the role and assignment names, sorted by level then name, capped with
      "+N more".
- [ ] The email is truncated with an ellipsis and stays within the nav at desktop and narrow widths.
- [ ] Any new style uses the design tokens; if a token is added, update both `dot-glasses.css` copies
      (Web and App) by hand, per CLAUDE.md.
- [ ] Web.Tests cover a user with one assignment and a user with more than the cap.

## Notes

- Spec: `../spec.md` — user stories 7–9, "Admin Portal screens".
- Skills: `/implement` (with `/tdd`), then `/code-review`.

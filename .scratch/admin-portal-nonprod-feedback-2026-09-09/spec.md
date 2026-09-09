# Admin Portal — Non-Prod Deployment Feedback — 2026-09-09

Source: manual testing of the deployed Admin Portal in the non-production Azure environment,
reported by Joe 2026-09-09 (six points), plus three more reported the same day after the first
batch was triaged. Nine distinct points total, split one issue per point per this repo's
issue-tracker convention (`docs/agents/issue-tracker.md`).

**Triage complete as of 2026-09-09.** All nine points were reproducible or directly groundable
against the current codebase (no reporter follow-up needed), so all nine are fully specified and
`ready-for-agent`. No grilling session was needed — each point resolved to a single, small,
well-scoped fix once traced to source.

## Active issues — `ready-for-agent` (9)

01. Login page doesn't redirect an already-authenticated user back to the dashboard
02. "No email sending is wired up yet" banner is stale now that ACS delivers real email in a
    deployed environment
03. Two buttons bypass the `dg-btn` design-system classes entirely (underline + alignment
    inconsistency)
04. Reference Data: Coating pairings/exclusions rows don't match the sandy-badge treatment of
    every other reference-data tablet, and the row icons are small across the board
05. Admin Portal main content area is capped at `max-width:1440px` instead of using the full
    viewport
06. Leftover scaffold boilerplate (`body { margin-bottom: 60px; }` in `site.css`) pushes the
    sidebar/content short of the bottom of the viewport
07. Lens strength coating availability grid full-page-reloads on every single checkbox toggle —
    needs batching into one real form with one Save
08. Sidebar nav collapses into a horizontally-scrolling row below 991px instead of a hamburger
    toggle
09. Sign-out icon is small and uses a glyph nobody reads as "sign out"

No cross-dependencies — all nine can be picked up independently and in any order. Ticket 09 notes
a soft ordering suggestion (check its chip layout after 08 lands, since both touch the user chip's
surroundings) but doesn't hard-block on it.

## Notes for whoever picks these up

- Issues 05 and 06 touch adjacent layout files (`dot-glasses.css` / `site.css` /
  `_Layout.cshtml`) but are two distinct reported defects (main-content width vs. body spacing) —
  see this repo's grouping convention in `docs/agents/issue-tracker.md`; they were kept as
  separate tickets rather than combined.
- Issue 03's fix (bringing the two orphaned buttons into the `dg-btn` system) is very likely the
  entire root cause of the reported "some buttons have underlines, some don't, some centered,
  some left-aligned" — a scan of every button in `src/DotGlasses.Web/Views/**/*.cshtml` found the
  `dg-btn`/`dg-btn-primary`/`dg-btn-ghost` classes already applied consistently everywhere else,
  including every full-width button already pairing `width:100%` with an explicit
  `justify-content:center`. If a wider inconsistency turns up during implementation that these two
  buttons don't explain, treat that as new information and re-open rather than force-fitting it
  into this ticket.

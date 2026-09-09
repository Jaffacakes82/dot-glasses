# Offline status, offline org scope, and Dashboard scoping — 2026-09-09

Source: further manual testing across the Field App and Admin Portal, reported by Joe 2026-09-09
after the previous feedback round's PR merged. Three distinct points, split one issue per point
per this repo's issue-tracker convention (`docs/agents/issue-tracker.md`).

**Triage complete as of 2026-09-09.** All three were reproducible or directly groundable against
the current codebase — including one (ticket 03) confirmed with a real Postgres-backed test written
during triage, not just static reading — so all three are fully specified and `ready-for-agent`.

## Active issues — `ready-for-agent` (3)

01. Field App's online/offline status never updates after the page first loads — no `offline`
    browser-event listener exists at all, and nothing periodically re-checks it for display.
02. Field App loses track of which organisation is active while offline — Home and Settings both
    require a live API call to know the active org, unlike the token and reference data, which
    both survive offline via an IndexedDB cache.
03. Admin Portal Dashboard's "Top performing" widget ranks unattributable sales (a DGI-level sale,
    or any row with no Country ancestor) as if "Unknown country" / "No retailer" were a real
    competing entity — including the `#1` highlight when it happens to have the most sales.
    Confirmed via a real test against Postgres during triage: the row resolves *honestly*
    (`OrgTreeLookup` already returns "No retailer"/"Unknown country", not a wrong real name) — the
    bug is that the Dashboard ranking includes that honest-but-empty bucket as a ranked entry at
    all, the same class of thing `IsTrainingOrg` exclusion already exists to prevent.

## Notes for whoever picks these up

- Decision on ticket 03 (asked directly, not assumed): **exclude** unattributable rows from the
  Top Retailers/Top Countries rankings entirely, the same treatment training orgs already get —
  don't keep them visible under a more honestly-labelled bucket. They still count toward the
  overall Dashboard totals (tests/sales/conversion) — only the two ranking widgets change.
- Tickets 01 and 02 are both Field App connectivity gaps but touch different files/mechanisms (a
  missing browser event listener + UI refresh vs. a missing IndexedDB cache) — kept separate.
- No cross-dependencies — all three can be picked up independently and in any order.

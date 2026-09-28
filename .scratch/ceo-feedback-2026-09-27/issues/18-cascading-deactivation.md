# 18 — Deactivating and reactivating an org with sub-orgs

Type: grilling
Status: open
Blocked by: None

## Question

Should deactivating an org deactivate everything under it, and what does reactivating a child of a
still-deactivated parent do? The feedback doc proposes re-parenting to the nearest active ancestor;
the call leaned towards "transfer first, then deactivate" (transfer is Day 2).

## Context

- Feedback doc: cascade down, reactivate all; reactivating A2 under a deactivated A moves it to the
  nearest active ancestor.
- Today: only a leaf can be deactivated ("Deactivate the child nodes first").
- Paths are never reused (`OrganisationPathSegments`); re-parenting changes a path, which touches
  every stamped copy (see triage-2026-09-26 ticket 03).
- Feedback doc: confirm data from a deactivated org stays in the MI and the database.
- Bug: the "Deactivated orgs" section showed when empty, and its Reactivate button glitched the page.

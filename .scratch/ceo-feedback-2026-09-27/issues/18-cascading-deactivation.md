# 18 — Deactivating and reactivating an org with sub-orgs

Type: grilling
Status: resolved
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

## Answer

Decided by grilling, 2026-10-01. Transferring an organisation to a different parent stays Day 2.

**Deactivating**

- Deactivating an organisation deactivates it and every organisation beneath it, in one action.
  The "deactivate the child orgs first" rule goes.
- A confirmation first says how many organisations go with it, how many people lose access, and
  that unsent Field App records for those retail points will be refused.

**Reactivating**

- Reactivating an organisation restores it and everything that was deactivated along with it.
- An organisation beneath it that was deactivated separately, beforehand, stays deactivated.
- An organisation can be reactivated only when the organisation directly above it is active.
  Otherwise it is refused with "reactivate the organisation above it first". This closes today's
  gap, where a child reactivated under a deactivated parent shows as a tree of its own.
- Nothing is moved to another parent. To get one child back, reactivate the parent and deactivate
  the others.

**Data from a deactivated organisation**

- Tests, Leads and Sales recorded there stay in the database and keep counting in the dashboard,
  Event History and the other reports.
- Reports show the organisation's real name followed by "(deactivated)". Today the name lookup
  reads active organisations only, which (from the code, not yet seen on screen) would show
  "Unknown outlet".

**People**

- Assignments inside the deactivated part are kept, give no access while it is deactivated, and
  work again on reactivation. Nobody is suspended.
- The User Directory marks such an assignment "deactivated" (ticket 03).

**The "Deactivated orgs" strip**

- Lists the top organisation of each deactivated group with a count of those beneath it ("Kenya
  Retail Ltd and 14 beneath it") and one Reactivate button for the group.
- An organisation deactivated on its own inside an active parent is listed as today.

**The reported bug**

- The strip is already hidden when empty in the current code. The Reactivate glitch wasn't
  reproduced; retest it when this is built, since the strip is being reworked anyway.

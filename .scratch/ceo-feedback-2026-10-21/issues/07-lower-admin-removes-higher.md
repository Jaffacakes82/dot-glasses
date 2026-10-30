# 07 — A lower-ranking admin can remove a higher-ranking one

Type: grilling
Status: needs-triage
Blocked by: None

## Question

What stops an admin removing someone who outranks them from one of the admin's own
organisations — and what does "outranks" mean?

## Context

- Feedback: "A lower ranking admin should not be able to remove a higher ranking admin or user
  that is assigned to their organisation and sub-orgs." The screenshot shows an Admin at a
  Retailer/distributor removing a DGI-level admin's assignment to that Retailer/distributor;
  the page then said the person "is no longer in your scope".
- Confirmed in code: removing one assignment needs only that organisation in the caller's scope
  (`UserDirectoryController.cs:159–172`, `Organisations.ManageInScope`). The stricter
  `Users.ManageInScope` (every assignment in scope) applies to role, suspension and password
  reset only. The Organisations screen's unassign action follows the same per-org rule.
- The CLAUDE.md rule "a lower assignment never narrows what a higher one already grants" means
  the removed assignment cost the DGI admin nothing in access, but they lose being listed at
  that org (and, at a retail point, the ability to record there).

## Points to settle

- Rank: the target's highest assigned level vs the caller's? Admin above User at the same level?
- Blocked actions: removing an assignment only, or also adding one (assigning a DGI admin to a
  retail point they never asked for)?
- Simplest candidate: removing an assignment needs `Users.ManageInScope` (every one of the
  target's assignments in the caller's scope) — a DGI admin is never fully inside a lower
  admin's scope. Does that match "higher ranking" closely enough, or does a same-level peer with
  one extra assignment elsewhere need to be removable?
- Same rule on both paths: Edit user and the Organisations screen's unassign.
- Interplay with ticket 03 (a User who may add but not remove).

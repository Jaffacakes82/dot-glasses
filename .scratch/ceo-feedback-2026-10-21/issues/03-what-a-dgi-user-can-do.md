# 03 — What a DGI-level User can do

Type: grilling
Status: needs-triage
Blocked by: None

## Question

Should a User (not an Admin) assigned at DGI be able to invite users and assign lens sets below
DGI — and if so, exactly which actions, and does the same apply to a User at Country or
Retailer/distributor level?

## Context

- Feedback: a User at DGI "is unable to add Users or Admins at levels below me. For DGI level a
  user should be able to add users at countries and retail points, just not delete them. Most
  people will be users as I will not want them touching the preset catalogues, reference data or
  adding users DGI level."
- Feedback: "A user at DGI level should be able to assign pre-set catalogues to organisations and
  countries beneath DGI." (A third line, "User at DGI level should be able to", is unfinished —
  probably the same request.)
- Today every User Directory and Organisations write is Admin-only (`Users.ManageInScope`,
  `Organisations.ManageInScope`), and assigning a lens set needs `PresetCatalogue.Manage` — Admin
  at Country level or above. A User at DGI sees the Access Denied page.
- The CEO marks a "Manager" tier or custom roles as Day 2 (see the map's Out of scope). This
  ticket is about the two roles that exist.

## Points to settle

- Invite: may a User invite an **Admin**, or only Users? (The feedback says "Users or Admins".)
  An invited Admin would hold more power than the User who invited them.
- "Add at countries and retail points, just not delete": which actions count as delete —
  suspend, remove an assignment, reset a password, change a role? Can the User edit a user they
  invited?
- Lens sets: assign/unassign only, or create and edit too? The feedback says Users should not
  touch "the preset catalogues", which reads as: assign yes, edit no.
- Does the rule depend on level (DGI only) or apply to a User at any level, for their own scope?
- Does a User get the Organisations writes too (add sub-org, rename, training flag)? Ticket 05
  assumes whoever creates an org can give it a lens set.
- Relation to ticket 07 (rank when removing someone).

## Related

- Ticket 05 (lens sets on the Organisations screen) is blocked on this.
- ADR-0006, `CLAUDE.md` RBAC table.

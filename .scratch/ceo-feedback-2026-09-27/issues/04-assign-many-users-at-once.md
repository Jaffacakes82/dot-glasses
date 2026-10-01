# 04 — Assigning several users to an org at once

Type: grilling
Status: resolved
Blocked by: 01

## Question

Should the Organisations screen's "Assign users" take several users in one go, and what does each
assignment grant under the multi-org model?

## Context

- Feedback doc: "Assign multiple users to orgs at once - Day 1 but not high priority".

## Answer

Decided by grilling, 2026-10-01. The rules sit on ADR-0006; nothing here changes it.

**The Assign users dialog (Organisations screen)**

- The single dropdown becomes a checkbox list of users with a filter box. One submit assigns
  everyone ticked to the selected org.
- The batch is one transaction: all assigned, or none.
- One org per submit. Several orgs for one user is the Edit user page (ticket 03).
- The list offers **Active users only**, and leaves out users already assigned to this org.
  Invited and Suspended users are not offered here; their orgs can still be changed on the Edit
  user page.
- A user the admin can't see in the User Directory can't be assigned; someone higher up does it.

**Showing what an assignment grants**

- Each user's role is shown beside their name, in the dialog and in the Assigned users list.
- One line under the dialog's title, by the org's level. At a retail point: they will be able to
  record here in the Field App. Higher up: Admin Portal access to this organisation and
  everything beneath it.

**Unassigning**

- Stays one user at a time (the × beside the name).
- At a retail point it asks for confirmation, with ticket 03's copy: the change applies at once,
  and unsent Field App records for that retail point will be refused.

**Assigning and unassigning yourself**

- An admin may assign themself to an org in their scope.
- An admin may unassign themself only if their scope is no smaller afterwards. Otherwise it is
  refused with "ask another admin". Ticket 03's answer was revised to match.

**Also**

- Each assign and unassign writes the same application log entry as ticket 03's edits.

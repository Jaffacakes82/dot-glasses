# 03 — Editing a user's role and org assignments

Type: grilling
Status: resolved
Blocked by: 01

## Question

What can an admin change about an existing user from the User Directory (role, assigned orgs,
anything the multi-org decision introduces), and how does the invite form's org picker change?

## Context

- Feedback doc: "Can't change user scope or role from the User Directory page."
- Feedback doc: invite form's org checkboxes are misaligned (checkbox above its label).
- Call: the "first checked becomes the primary" hint is to be removed.
- Today users can only be added to or removed from an org on the Organisations screen.
- From ticket 01 (ADR-0006):
  - "Primary org" is gone.
  - A user keeps at least one assignment; suspension removes all access.
  - One role per user.
  - An admin sees a user if any of that user's assignments is in the admin's scope. Changing the
    user's role, suspending them or resetting their password needs **all** of their assignments in
    scope. Adding or removing one assignment needs that org in scope.
  - Changes take effect on the user's next request.
  - Still open here: how the invite and edit forms present this.

## Answer

Decided by grilling, 2026-10-01. Every rule below sits on ADR-0006; nothing here changes it.

**The Edit user page**

- Each row of the User Directory gets an **Edit** link to a dedicated page, not a dialog. A
  refused save redirects back to that page with the message.
- Editable: **role**, **org assignments** and **full name**. Email is not editable; it is the
  sign-in identity.
- **One Save** applies everything together, in one transaction. This lets an admin swap a user's
  only org for another in one step.
- The save applies **only what the admin changed**. The form carries the role and assignment set
  it was loaded with, and the server adds and removes only the differences, so a stale form can't
  undo another admin's change.
- Invited, Active and Suspended users can all be edited. Editing a suspended user doesn't
  unsuspend them.
- Reset password and Suspend/Unsuspend stay as row buttons on the directory. The row has Edit,
  Reset password and Suspend.

**A user only partly in the admin's scope**

- Role is shown read-only, with a line saying it needs every one of the user's organisations to
  be in your scope.
- Out-of-scope assignments are shown as a count ("plus 2 organisations outside your scope"),
  never by name. The directory list already hides those names the same way.
- An admin may remove every assignment they can see, as long as the user keeps one elsewhere. The
  save returns to the directory with a message that the user is no longer in your scope.

**Who can edit whom**

- An admin can open their own Edit page (revised by the ticket 04 grilling, 2026-10-01, which
  replaced "an admin can't edit themself"):
  - Your own role is read-only, with a line saying another admin must change it. The server
    refuses a change to your own role, and refuses suspending yourself (a hole today, closed here).
  - Your own full name is editable.
  - You may add an assignment for yourself. The org must be in your scope, so this never widens
    what you can see. The usual case is a DGI admin adding a retail point to record there.
  - You may remove one of your own assignments only if your scope is no smaller afterwards, that
    is, the org sits beneath another assignment you keep. Anything that would shrink your scope is
    refused with "ask another admin". This keeps the last DGI admin from locking themself out.
  - The same rules apply to assigning and unassigning yourself on the Organisations screen.
- Admins can edit other admins under the existing all-assignments-in-scope rule, including a peer
  at the same org. No "strictly above" rule.

**The org picker, shared by Invite and Edit**

- An indented tree showing each org's level, with a filter box, labelled "Organisations". The
  checkbox sits beside its label.
- One line of help text: only a direct retail-point assignment lets someone record in the Field
  App; an assignment higher up gives Admin Portal access only.
- Picking an org and one beneath it is allowed and meaningful (DGI plus a retail point).
- A deactivated org is not offered for a new assignment. An existing assignment to one is shown
  ticked and marked "deactivated", can be unticked, and still counts toward "at least one".
- The invite dialog's stale "No email sending is wired up yet" note is removed.

**Confirmation**

- Save asks for confirmation only when a retail-point assignment is removed or an Admin is
  demoted. The copy names the consequence: the change applies at once, and unsent Field App
  records for that retail point will be refused.
- The edited user is not emailed.

**Elsewhere**

- The directory's "Scope" column is renamed "Organisations", since it lists assignments and the
  glossary's Scope means something wider.
- Assign and unassign stay on the Organisations screen as well. Ticket 04 builds on that side.
- Each saved change writes an application log entry: who made it, which user, and what changed.
  There is no change-history screen; that gap is noted in `docs/open-issues.md`.

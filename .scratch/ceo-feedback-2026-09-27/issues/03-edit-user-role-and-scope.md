# 03 — Editing a user's role and org assignments

Type: grilling
Status: open
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

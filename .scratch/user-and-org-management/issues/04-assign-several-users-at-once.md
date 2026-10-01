# 04 — Assign several users to an organisation at once

**What to build:** On the Organisations screen, the Assign users dialog becomes a filterable
checkbox list. One submit assigns everyone ticked to the selected organisation, all or nothing.
The dialog and the Assigned users list show each user's role, and the dialog says what the
assignment grants at this level.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Sonnet 5.5.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 01, 02.

## Acceptance criteria

- [ ] The dialog lists users as checkboxes with a filter box and posts a list of user ids for one
      organisation.
- [ ] All ticked users are assigned in one transaction through the execution strategy. Any
      refusal rolls back the whole batch.
- [ ] Only Active users not already assigned to this organisation are offered. The server refuses
      an Invited or Suspended user id.
- [ ] A user the admin can't see in the User Directory is not offered and is refused by the
      server; someone higher up assigns them.
- [ ] Each user's role is shown beside their name in the dialog and in the Assigned users list.
- [ ] A line under the title, by level. At a retail point: they will be able to record here in
      the Field App. Higher up: Admin Portal access to this organisation and everything beneath it.
- [ ] Unassign stays one user at a time, and asks for confirmation at a retail point with the Edit
      page's copy.
- [ ] Each assign and unassign writes the same structured log entry as ticket 01.
- [ ] Web.Tests cover: several users in one request; an Invited or Suspended id refused; one bad
      id leaves nobody assigned.

## Notes

- Spec: `../spec.md` — user stories 16–19; "Assign users dialog".
- Authorisation stays `Organisations.ManageInScope` against the target organisation.
- Skills: `/tdd`, then `/code-review`.

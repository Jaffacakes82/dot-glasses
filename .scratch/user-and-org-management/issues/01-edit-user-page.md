# 01 — Edit user page: role, org assignments and full name

**What to build:** Each User Directory row gets an Edit link to a page where an admin changes a
user's role, org assignments and full name, and saves them together. The save applies only what the
admin changed since the page loaded. A user partly outside the admin's scope shows a read-only role
and a count of hidden organisations.

**Blocked by:** None

**Status:** ready-for-agent

**Model:** Opus 5.5 — permission rules per change plus atomic Identity writes.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 02, 04 (all touch `UserAdminService`'s assignment writes).

## Acceptance criteria

- [ ] An Edit link on every row the admin can see, alongside Reset password and Suspend/Unsuspend.
- [ ] The page edits role, org assignments and full name. Email is shown and not editable.
- [ ] One Save, one transaction opened through the execution strategy; every `IdentityResult`
      checked (CLAUDE.md, `InviteAtomicityTests`).
- [ ] The form posts the role and assignment set it was loaded with; the server applies only the
      differences. An assignment added by someone else since is left alone.
- [ ] A role or name change needs every one of the target's assignments in scope; adding or
      removing an assignment needs that org in scope. The existing policies are reused.
- [ ] A user partly out of scope: role read-only with a reason, out-of-scope assignments shown as
      "plus N organisations outside your scope", never named and never changed.
- [ ] Removing every visible assignment is allowed when the user keeps one elsewhere, and returns
      to the directory with "no longer in your scope". With none left anywhere it is refused with
      the existing last-assignment message.
- [ ] A confirmation before saving when a retail-point assignment is removed or an Admin becomes a
      User, naming the consequence (applies at once; unsent Field App records for that retail
      point will be refused).
- [ ] An assignment to a deactivated organisation still counts toward "at least one".
- [ ] A refused save redirects back to the Edit page with the message.
- [ ] The edited user is not emailed.
- [ ] Invited, Active and Suspended users can all be edited; editing doesn't unsuspend.
- [ ] Each role, name or assignment change writes a structured log entry: acting user, target
      user, what changed.
- [ ] The directory's "Scope" column is headed "Organisations".
- [ ] Web.Tests cover the cases in the spec's "Edit" bullet.

## Notes

- Spec: `../spec.md` — user stories 1–7, 15, 30; "Edit user page", "Logging".
- The org picker here can start as the existing flat list; ticket 03 replaces it.
- The `ChangeRole` endpoint added earlier with no form can be folded into this save or removed.
- Skills: `/tdd`, then `/code-review`.

# 01 — Edit user page: role, org assignments and full name

**What to build:** Each User Directory row gets an Edit link to a page where an admin changes a
user's role, org assignments and full name, and saves them together. The save applies only what the
admin changed since the page loaded. A user partly outside the admin's scope shows a read-only role
and a count of hidden organisations.

**Blocked by:** None

**Status:** resolved

**Model:** Opus 5.5 — permission rules per change plus atomic Identity writes.

**Seam:** `DotGlasses.Web.Tests` over HTTP.

**Don't run alongside:** 02, 04 (all touch `UserAdminService`'s assignment writes).

## Acceptance criteria

- [x] An Edit link on every row the admin can see, alongside Reset password and Suspend/Unsuspend.
- [x] The page edits role, org assignments and full name. Email is shown and not editable.
- [x] One Save, one transaction opened through the execution strategy; every `IdentityResult`
      checked (CLAUDE.md, `InviteAtomicityTests`).
- [x] The form posts the role and assignment set it was loaded with; the server applies only the
      differences. An assignment added by someone else since is left alone.
- [x] A role or name change needs every one of the target's assignments in scope; adding or
      removing an assignment needs that org in scope. The existing policies are reused.
- [x] A user partly out of scope: role read-only with a reason, out-of-scope assignments shown as
      "plus N organisations outside your scope", never named and never changed.
- [x] Removing every visible assignment is allowed when the user keeps one elsewhere, and returns
      to the directory with "no longer in your scope". With none left anywhere it is refused with
      the existing last-assignment message.
- [x] A confirmation before saving when a retail-point assignment is removed or an Admin becomes a
      User, naming the consequence (applies at once; unsent Field App records for that retail
      point will be refused).
- [x] An assignment to a deactivated organisation still counts toward "at least one".
- [x] A refused save redirects back to the Edit page with the message.
- [x] The edited user is not emailed.
- [x] Invited, Active and Suspended users can all be edited; editing doesn't unsuspend.
- [x] Each role, name or assignment change writes a structured log entry: acting user, target
      user, what changed.
- [x] The directory's "Scope" column is headed "Organisations".
- [x] Web.Tests cover the cases in the spec's "Edit" bullet.

## Notes

- Spec: `../spec.md` — user stories 1–7, 15, 30; "Edit user page", "Logging".
- The org picker here can start as the existing flat list; ticket 03 replaces it.
- The `ChangeRole` endpoint added earlier with no form can be folded into this save or removed.
- Skills: `/tdd`, then `/code-review`.

## Comments

**2026-10-01 — built.** `UserDirectoryController.Edit` (GET/POST), `Views/UserDirectory/Edit.cshtml`,
`UserAdminService.GetForEditAsync`/`UpdateAsync`, `UserEditPlan.Diff` (the differences), and the
per-change checks in the controller. `ChangeRole` is kept as a role-only endpoint and now goes
through the same `UpdateAsync`. Tests: `EditUserTests`, `UserEditPlanTests`, `AccessAuditTests`.

Two things decided while building, not in the ticket:

- **The Edit page is for Admins only.** A User-role account sees the directory but gets
  access-denied on the page: every change it offers is an Admin's.
- **A name change needs every assignment in scope**, the same as a role change (the spec says so;
  the ticket's wording was looser).

The confirmation before saving is a browser confirm built from the form's own data; the server
applies the change either way.
